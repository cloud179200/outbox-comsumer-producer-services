using ConsumerService.Services;
using ConsumerService.Models;

namespace ConsumerService.BackgroundServices;

public class ConsumerBackgroundService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<ConsumerBackgroundService> _logger;
  private readonly IConfiguration _configuration;
  private readonly string _serviceId;
  private readonly string _instanceId;

  public ConsumerBackgroundService(
      IServiceProvider serviceProvider,
      ILogger<ConsumerBackgroundService> logger,
      IConfiguration configuration)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _configuration = configuration;

    // Get service identification from environment
    _serviceId = Environment.GetEnvironmentVariable("SERVICE_ID")
        ?? Environment.GetEnvironmentVariable("CONSUMER_SERVICE_ID")
        ?? $"consumer-{Environment.MachineName}";
    _instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
        ?? $"{_serviceId}-{Guid.NewGuid():N}";
  }
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Consumer background service started for {ServiceId} (Instance: {InstanceId})",
        _serviceId, _instanceId);

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

    // Start heartbeat task
    var heartbeatTask = StartHeartbeatService(stoppingToken);
    consumerTasks.Add(heartbeatTask);

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

  private async Task StartHeartbeatService(CancellationToken cancellationToken)
  {
    var heartbeatInterval = _configuration.GetValue<int>("ConsumerHeartbeatIntervalMs", 30000);
    var producerServiceUrl = _configuration["ProducerService:BaseUrl"] ?? "http://localhost:5299";

    _logger.LogInformation("Starting heartbeat service for Consumer Service {ServiceId}", _serviceId);

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        using var httpClient = new HttpClient();

        var heartbeatRequest = new
        {
          ServiceId = _serviceId,
          InstanceId = _instanceId,
          Status = "Active",
          HealthStatus = "Healthy",
          StatusMessage = "Consumer service running normally",
          HealthData = CollectHealthData()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(heartbeatRequest);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{producerServiceUrl}/api/agents/consumers/heartbeat", content);

        if (response.IsSuccessStatusCode)
        {
          _logger.LogDebug("Heartbeat sent successfully for Consumer Service {ServiceId}", _serviceId);
        }
        else
        {
          _logger.LogWarning("Failed to send heartbeat for Consumer Service {ServiceId}. Status: {Status}",
              _serviceId, response.StatusCode);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error sending heartbeat for Consumer Service {ServiceId}", _serviceId);
      }

      await Task.Delay(heartbeatInterval, cancellationToken);
    }

    _logger.LogInformation("Heartbeat service stopped for Consumer Service {ServiceId}", _serviceId);
  }

  private Dictionary<string, object> CollectHealthData()
  {
    try
    {
      var healthData = new Dictionary<string, object>
      {
        ["timestamp"] = DateTime.UtcNow.ToString("O"),
        ["uptime"] = Environment.TickCount64,
        ["machineName"] = Environment.MachineName,
        ["processId"] = Environment.ProcessId,
        ["workingSet"] = Environment.WorkingSet,
        ["gcMemory"] = GC.GetTotalMemory(false)
      };

      return healthData;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Could not collect health data");
      return new Dictionary<string, object>
      {
        ["timestamp"] = DateTime.UtcNow.ToString("O"),
        ["error"] = ex.Message
      };
    }
  }
}
