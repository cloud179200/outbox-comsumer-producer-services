using Quartz;
using ProducerService.Services;

namespace ProducerService.Jobs;

[DisallowConcurrentExecution]
public class CleanupOldMessagesJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<CleanupOldMessagesJob> _logger;

  public CleanupOldMessagesJob(IServiceProvider serviceProvider, ILogger<CleanupOldMessagesJob> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task Execute(IJobExecutionContext context)
  {
    try
    {
      using var scope = _serviceProvider.CreateScope();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

      // Clean up old acknowledged/failed messages older than 7 days
      var retentionDays = 7;
      var cleanupResult = await outboxService.CleanupOldMessagesAsync(retentionDays);

      if (cleanupResult > 0)
      {
        _logger.LogInformation("Cleaned up {Count} old messages older than {Days} days", cleanupResult, retentionDays);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during cleanup of old messages");
    }
  }
}
