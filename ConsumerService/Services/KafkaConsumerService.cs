using Confluent.Kafka;
using Newtonsoft.Json;
using ConsumerService.Models;

namespace ConsumerService.Services;

public interface IKafkaConsumerService
{
  Task StartConsumingAsync(string[] topics, string consumerGroup, CancellationToken cancellationToken);
}

public class KafkaConsumerService : IKafkaConsumerService
{
  private readonly ILogger<KafkaConsumerService> _logger;
  private readonly IServiceProvider _serviceProvider;
  private readonly IConfiguration _configuration;

  public KafkaConsumerService(ILogger<KafkaConsumerService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
  {
    _logger = logger;
    _serviceProvider = serviceProvider;
    _configuration = configuration;
  }

  public async Task StartConsumingAsync(string[] topics, string consumerGroup, CancellationToken cancellationToken)
  {
    var config = new ConsumerConfig
    {
      BootstrapServers = _configuration.GetConnectionString("Kafka") ?? "localhost:9092",
      GroupId = consumerGroup,
      ClientId = $"outbox-consumer-{consumerGroup}",
      AutoOffsetReset = AutoOffsetReset.Earliest,
      EnableAutoCommit = false, // Manual commit for better control
      SessionTimeoutMs = 30000,
      HeartbeatIntervalMs = 10000,
      MaxPollIntervalMs = 300000
    };

    using var consumer = new ConsumerBuilder<string, string>(config)
        .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
        .SetPartitionsAssignedHandler((c, partitions) =>
        {
          _logger.LogInformation("Consumer {ConsumerGroup} assigned partitions: [{Partitions}]",
                  consumerGroup, string.Join(", ", partitions));
        })
        .Build();

    try
    {
      consumer.Subscribe(topics);
      _logger.LogInformation("Consumer {ConsumerGroup} subscribed to topics: [{Topics}]",
          consumerGroup, string.Join(", ", topics));

      while (!cancellationToken.IsCancellationRequested)
      {
        try
        {
          var consumeResult = consumer.Consume(cancellationToken);
          if (consumeResult?.Message != null)
          {
            await ProcessMessage(consumeResult, consumerGroup);
            consumer.Commit(consumeResult);
          }
        }
        catch (ConsumeException ex)
        {
          _logger.LogError(ex, "Error consuming message for consumer group {ConsumerGroup}", consumerGroup);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Unexpected error in consumer {ConsumerGroup}", consumerGroup);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Fatal error in Kafka consumer {ConsumerGroup}", consumerGroup);
    }
    finally
    {
      consumer.Close();
      _logger.LogInformation("Consumer {ConsumerGroup} closed", consumerGroup);
    }
  }

  private async Task ProcessMessage(ConsumeResult<string, string> consumeResult, string consumerGroup)
  {
    try
    {
      _logger.LogInformation("Received message {Key} from topic {Topic} for consumer group {ConsumerGroup}",
          consumeResult.Message.Key, consumeResult.Topic, consumerGroup);      // Parse the message
      var messageData = JsonConvert.DeserializeObject<dynamic>(consumeResult.Message.Value);
      var messageId = messageData?.MessageId?.ToString() ?? "";
      var content = messageData?.Content?.ToString();
      var producerServiceId = messageData?.ProducerServiceId?.ToString() ?? "";
      var producerInstanceId = messageData?.ProducerInstanceId?.ToString() ?? ""; if (string.IsNullOrEmpty(messageId))
      {
        _logger.LogWarning("Received message without MessageId from topic {Topic}", consumeResult.Topic);
        return;
      }      // Create consumer message
      var consumerMessage = new ConsumerMessage
      {
        MessageId = messageId,
        Topic = consumeResult.Topic,
        Content = content ?? consumeResult.Message.Value,
        ConsumerGroup = consumerGroup,
        ProducerServiceId = producerServiceId,
        ProducerInstanceId = producerInstanceId
      };

      // Process the message using scoped services
      using var scope = _serviceProvider.CreateScope();
      var messageProcessor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
      var success = await messageProcessor.ProcessMessageAsync(consumerMessage);      // Send acknowledgment to producer
      await SendAcknowledgment(messageId, consumerGroup, success);

      _logger.LogInformation("Message {MessageId} processed successfully by consumer group {ConsumerGroup}",
          (string)messageId, (string)consumerGroup);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing message {Key} from topic {Topic}",
          consumeResult.Message.Key, consumeResult.Topic);

      // Try to get messageId for failed acknowledgment
      try
      {
        var messageData = JsonConvert.DeserializeObject<dynamic>(consumeResult.Message.Value);
        var failedMessageId = messageData?.MessageId?.ToString();
        if (!string.IsNullOrEmpty(failedMessageId))
        {
          await SendAcknowledgment(failedMessageId, consumerGroup, false, ex.Message);
        }
      }
      catch
      {
        _logger.LogError("Could not send failure acknowledgment for message {Key}", consumeResult.Message.Key);
      }
    }
  }

  private async Task SendAcknowledgment(string messageId, string consumerGroup, bool success, string? errorMessage = null)
  {
    try
    {
      using var scope = _serviceProvider.CreateScope();
      var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

      var producerUrl = _configuration["ProducerService:BaseUrl"] ?? "http://localhost:5299";
      var acknowledgmentRequest = new AcknowledgmentRequest
      {
        MessageId = messageId,
        ConsumerGroup = consumerGroup,
        Success = success,
        ErrorMessage = errorMessage
      };

      var json = JsonConvert.SerializeObject(acknowledgmentRequest);
      var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

      var response = await httpClient.PostAsync($"{producerUrl}/api/messages/acknowledge", content);

      if (response.IsSuccessStatusCode)
      {
        _logger.LogDebug("Acknowledgment sent for message {MessageId} with status {Success}",
            messageId, success);
      }
      else
      {
        _logger.LogWarning("Failed to send acknowledgment for message {MessageId}. Status: {StatusCode}",
            messageId, response.StatusCode);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending acknowledgment for message {MessageId}", messageId);
    }
  }
}
