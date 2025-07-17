using ConsumerService.Services;

namespace ConsumerService.BackgroundServices;

public class ConsumerBackgroundService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<ConsumerBackgroundService> _logger;
  private readonly IConfiguration _configuration;

  public ConsumerBackgroundService(
      IServiceProvider serviceProvider,
      ILogger<ConsumerBackgroundService> logger,
      IConfiguration configuration)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _configuration = configuration;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Consumer background service started");

    // Get consumer group configuration from appsettings
    var consumerGroups = _configuration.GetSection("ConsumerGroups").Get<ConsumerGroupConfig[]>()
        ?? new ConsumerGroupConfig[]
        {
                new ConsumerGroupConfig
                {
                    GroupName = "default-consumer-group",
                    Topics = new[] { "user-events", "order-events" }
                },
                new ConsumerGroupConfig
                {
                    GroupName = "analytics-group",
                    Topics = new[] { "analytics-events" }
                },
                new ConsumerGroupConfig
                {
                    GroupName = "notification-group",
                    Topics = new[] { "notification-events" }
                }
        };

    // Start multiple consumer tasks
    var consumerTasks = new List<Task>();

    foreach (var consumerGroup in consumerGroups)
    {
      var task = StartConsumerGroup(consumerGroup, stoppingToken);
      consumerTasks.Add(task);
    }

    try
    {
      await Task.WhenAll(consumerTasks);
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Consumer background service cancelled");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in consumer background service");
    }

    _logger.LogInformation("Consumer background service stopped");
  }

  private async Task StartConsumerGroup(ConsumerGroupConfig config, CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        using var scope = _serviceProvider.CreateScope();
        var kafkaConsumerService = scope.ServiceProvider.GetRequiredService<IKafkaConsumerService>();

        _logger.LogInformation("Starting consumer group {ConsumerGroup} for topics: [{Topics}]",
            config.GroupName, string.Join(", ", config.Topics));

        await kafkaConsumerService.StartConsumingAsync(config.Topics, config.GroupName, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in consumer group {ConsumerGroup}, restarting in 30 seconds", config.GroupName);

        try
        {
          await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
      }
    }

    _logger.LogInformation("Consumer group {ConsumerGroup} stopped", config.GroupName);
  }
}

public class ConsumerGroupConfig
{
  public string GroupName { get; set; } = string.Empty;
  public string[] Topics { get; set; } = Array.Empty<string>();
}
