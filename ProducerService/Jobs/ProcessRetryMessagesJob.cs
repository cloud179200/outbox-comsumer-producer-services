using Quartz;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Jobs;

[DisallowConcurrentExecution]
public class ProcessRetryMessagesJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<ProcessRetryMessagesJob> _logger;

  public ProcessRetryMessagesJob(IServiceProvider serviceProvider, ILogger<ProcessRetryMessagesJob> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task Execute(IJobExecutionContext context)
  {
    _logger.LogDebug("Processing retry messages job started");

    try
    {
      using var scope = _serviceProvider.CreateScope();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
      var kafkaService = scope.ServiceProvider.GetRequiredService<IKafkaProducerService>();
      var topicRegistrationService = scope.ServiceProvider.GetRequiredService<ITopicRegistrationService>();
      var agentService = scope.ServiceProvider.GetRequiredService<IAgentService>();

      // Get all active consumer groups from database
      var allConsumerGroups = await topicRegistrationService.GetAllConsumerGroupsAsync();
      var activeConsumerGroups = allConsumerGroups.Where(cg => cg.IsActive).Select(cg => cg.ConsumerGroupName).Distinct();

      foreach (var consumerGroup in activeConsumerGroups)
      {
        var acknowledgmentTimeout = TimeSpan.FromMinutes(5);
        var unacknowledgedMessages = await outboxService.GetUnacknowledgedMessagesAsync(consumerGroup, acknowledgmentTimeout);

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
              // Get active consumer services for targeting
              var activeConsumers = await agentService.GetActiveConsumerAgentsAsync();
              var targetConsumer = activeConsumers
                  .Where(c => c.AssignedConsumerGroups.Contains(consumerGroup))
                  .FirstOrDefault();

              _logger.LogInformation("Retrying message {MessageId} for consumer group {ConsumerGroup} (attempt {RetryCount})",
                  message.Id, consumerGroup, message.RetryCount + 1);

              // Create targeted retry message
              await outboxService.CreateRetryMessageAsync(message, targetConsumer?.ServiceId);
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
      _logger.LogError(ex, "Error in ProcessRetryMessagesJob");
    }

    _logger.LogDebug("Processing retry messages job completed");
  }
}
