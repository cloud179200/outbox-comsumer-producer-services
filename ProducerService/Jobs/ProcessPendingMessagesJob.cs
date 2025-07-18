using Quartz;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Jobs;

[DisallowConcurrentExecution]
public class ProcessPendingMessagesJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<ProcessPendingMessagesJob> _logger;

  public ProcessPendingMessagesJob(IServiceProvider serviceProvider, ILogger<ProcessPendingMessagesJob> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task Execute(IJobExecutionContext context)
  {
    _logger.LogDebug("Processing pending messages job started");

    try
    {
      using var scope = _serviceProvider.CreateScope();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
      var kafkaService = scope.ServiceProvider.GetRequiredService<IKafkaProducerService>();

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
      _logger.LogError(ex, "Error in ProcessPendingMessagesJob");
    }

    _logger.LogDebug("Processing pending messages job completed");
  }
}
