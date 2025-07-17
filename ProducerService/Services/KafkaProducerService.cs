using Confluent.Kafka;
using Newtonsoft.Json;
using ProducerService.Models;

namespace ProducerService.Services;

public interface IKafkaProducerService
{
  Task<bool> SendMessageAsync(OutboxMessage message);
}

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
  private readonly IProducer<string, string> _producer;
  private readonly ILogger<KafkaProducerService> _logger;
  private readonly IOutboxService _outboxService;

  public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger, IOutboxService outboxService)
  {
    _logger = logger;
    _outboxService = outboxService;

    var config = new ProducerConfig
    {
      BootstrapServers = configuration.GetConnectionString("Kafka") ?? "localhost:9092",
      ClientId = "outbox-producer",
      Acks = Acks.All,
      MessageTimeoutMs = 30000,
      EnableIdempotence = true,
      RetryBackoffMs = 1000,
      MessageSendMaxRetries = 3
    };

    _producer = new ProducerBuilder<string, string>(config)
        .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Reason}", e.Reason))
        .Build();
  }

  public async Task<bool> SendMessageAsync(OutboxMessage message)
  {
    try
    {
      var kafkaMessage = new Message<string, string>
      {
        Key = message.Id,
        Value = JsonConvert.SerializeObject(new
        {
          MessageId = message.Id,
          Content = message.Message,
          ConsumerGroup = message.ConsumerGroup,
          Timestamp = message.CreatedAt
        }),
        Headers = new Headers
                {
                    { "MessageId", System.Text.Encoding.UTF8.GetBytes(message.Id) },
                    { "ConsumerGroup", System.Text.Encoding.UTF8.GetBytes(message.ConsumerGroup) },
                    { "CreatedAt", System.Text.Encoding.UTF8.GetBytes(message.CreatedAt.ToString("O")) }
                }
      };

      var deliveryResult = await _producer.ProduceAsync(message.Topic, kafkaMessage);

      if (deliveryResult.Status == PersistenceStatus.Persisted)
      {
        await _outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Sent);
        _logger.LogInformation("Message {MessageId} sent to Kafka topic {Topic} successfully",
            message.Id, message.Topic);
        return true;
      }
      else
      {
        await _outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Failed,
            $"Message not persisted. Status: {deliveryResult.Status}");
        _logger.LogWarning("Message {MessageId} was not persisted to Kafka. Status: {Status}",
            message.Id, deliveryResult.Status);
        return false;
      }
    }
    catch (ProduceException<string, string> ex)
    {
      await _outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Failed, ex.Message);
      _logger.LogError(ex, "Error sending message {MessageId} to Kafka topic {Topic}",
          message.Id, message.Topic);
      return false;
    }
    catch (Exception ex)
    {
      await _outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Failed, ex.Message);
      _logger.LogError(ex, "Unexpected error sending message {MessageId} to Kafka", message.Id);
      return false;
    }
  }

  public void Dispose()
  {
    _producer?.Dispose();
  }
}
