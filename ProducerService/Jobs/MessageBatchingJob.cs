using Quartz;
using ProducerService.Services;
using ProducerService.Models;
using System.Collections.Concurrent;

namespace ProducerService.Jobs;

[DisallowConcurrentExecution]
public class MessageBatchingJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<MessageBatchingJob> _logger;

  public MessageBatchingJob(IServiceProvider serviceProvider, ILogger<MessageBatchingJob> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task Execute(IJobExecutionContext context)
  {
    _logger.LogDebug("Message batching job started");

    try
    {
      using var scope = _serviceProvider.CreateScope();
      var batchingService = scope.ServiceProvider.GetRequiredService<IQuartzMessageBatchingService>();

      // Flush any pending batches
      await batchingService.FlushBatchAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in MessageBatchingJob");
    }

    _logger.LogDebug("Message batching job completed");
  }
}
