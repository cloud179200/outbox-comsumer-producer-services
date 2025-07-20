using ProducerService.Services;

namespace ProducerService.HostedServices;

public class MessageBatchingHostedService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<MessageBatchingHostedService> _logger;

  public MessageBatchingHostedService(IServiceProvider serviceProvider, ILogger<MessageBatchingHostedService> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Message Batching Hosted Service starting");

    try
    {
      var batchingService = _serviceProvider.GetRequiredService<IMessageBatchingService>();
      await batchingService.StartBatchProcessingAsync(stoppingToken);
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Message Batching Hosted Service cancelled");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Message Batching Hosted Service failed");
      throw;
    }
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Message Batching Hosted Service stopping");
    await base.StopAsync(cancellationToken);
  }
}
