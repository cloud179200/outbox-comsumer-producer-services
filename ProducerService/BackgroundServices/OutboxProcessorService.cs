using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.BackgroundServices;

public class OutboxProcessorService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<OutboxProcessorService> _logger;
  private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(5);
  private readonly TimeSpan _acknowledgmentTimeout = TimeSpan.FromMinutes(5);
  private readonly string _currentServiceId;
  private readonly string _currentInstanceId;

  public OutboxProcessorService(IServiceProvider serviceProvider, ILogger<OutboxProcessorService> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;

    // Get current service identification from environment
    _currentServiceId = Environment.GetEnvironmentVariable("SERVICE_ID")
        ?? Environment.GetEnvironmentVariable("PRODUCER_SERVICE_ID")
        ?? $"producer-{Environment.MachineName}";
    _currentInstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
        ?? $"{_currentServiceId}-{Guid.NewGuid():N}";
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Outbox processor service started");

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var scope = _serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var kafkaService = scope.ServiceProvider.GetRequiredService<IKafkaProducerService>();        // Process pending messages for this producer service only
        await ProcessPendingMessages(outboxService, kafkaService);

        // Check for unacknowledged messages that might need retry
        await ProcessUnacknowledgedMessages(outboxService, kafkaService);

        // Clean up old acknowledged/failed messages
        await CleanupOldMessages(outboxService);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in outbox processor service");
      }

      await Task.Delay(_processingInterval, stoppingToken);
    }

    _logger.LogInformation("Outbox processor service stopped");
  }

  private async Task ProcessPendingMessages(IOutboxService outboxService, IKafkaProducerService kafkaService)
  {
    try
    {
      var pendingMessages = await outboxService.GetPendingMessagesAsync(50);

      if (pendingMessages.Any())
      {
        _logger.LogInformation("Processing {Count} pending messages", pendingMessages.Count);
      }

      foreach (var message in pendingMessages)
      {
        try
        {
          var success = await kafkaService.SendMessageAsync(message);
          if (!success)
          {
            _logger.LogWarning("Failed to send message {MessageId} to Kafka", message.Id);
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
          await outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Failed, ex.Message);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing pending messages");
    }
  }
  private async Task ProcessUnacknowledgedMessages(IOutboxService outboxService, IKafkaProducerService kafkaService)
  {
    try
    {
      using var scope = _serviceProvider.CreateScope();
      var topicRegistrationService = scope.ServiceProvider.GetRequiredService<ITopicRegistrationService>();

      // Get all active consumer groups from database
      var allConsumerGroups = await topicRegistrationService.GetAllConsumerGroupsAsync();
      var activeConsumerGroups = allConsumerGroups.Where(cg => cg.IsActive).Select(cg => cg.ConsumerGroupName).Distinct();

      foreach (var consumerGroup in activeConsumerGroups)
      {
        var unacknowledgedMessages = await outboxService.GetUnacknowledgedMessagesAsync(consumerGroup, _acknowledgmentTimeout);

        if (unacknowledgedMessages.Any())
        {
          _logger.LogWarning("Found {Count} unacknowledged messages for consumer group {ConsumerGroup}",
              unacknowledgedMessages.Count, consumerGroup);
        }

        foreach (var message in unacknowledgedMessages)
        {
          try
          {
            if (message.RetryCount < 3) // Max retry attempts
            {
              _logger.LogInformation("Retrying message {MessageId} for consumer group {ConsumerGroup} (attempt {RetryCount})",
                  message.Id, consumerGroup, message.RetryCount + 1);

              // Reset status to pending for retry
              await outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Pending);
            }
            else
            {
              _logger.LogWarning("Message {MessageId} exceeded max retry attempts, marking as failed", message.Id);
              await outboxService.UpdateMessageStatusAsync(message.Id, OutboxMessageStatus.Failed,
                  "Maximum retry attempts exceeded");
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error processing unacknowledged message {MessageId}", message.Id);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing unacknowledged messages");
    }
  }

  private async Task CleanupOldMessages(IOutboxService outboxService)
  {
    try
    {
      // This is a simplified cleanup - in a real implementation, you'd want to 
      // periodically clean up old acknowledged or permanently failed messages
      // based on a retention policy

      _logger.LogDebug("Cleanup task executed");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during cleanup");
    }
  }
}
