using Quartz;

namespace ConsumerService.Jobs;

/// <summary>
/// Scheduled job that maintains consumer agent registration with the producer service.
/// Sends periodic heartbeats with health data and consumer group assignments for service discovery.
/// Prevents concurrent execution to avoid duplicate heartbeat transmissions.
/// </summary>
[DisallowConcurrentExecution]
public class ConsumerHeartbeatJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<ConsumerHeartbeatJob> _logger;
  private readonly IConfiguration _configuration;

  /// <summary>
  /// Initializes the Consumer Heartbeat Job with required dependencies.
  /// Sets up service provider, logging, and configuration access for heartbeat operations.
  /// </summary>
  /// <param name="serviceProvider">Service provider for dependency resolution</param>
  /// <param name="logger">Logger for tracking heartbeat operations</param>
  /// <param name="configuration">Configuration for producer service URL and settings</param>
  public ConsumerHeartbeatJob(
      IServiceProvider serviceProvider,
      ILogger<ConsumerHeartbeatJob> logger,
      IConfiguration configuration)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _configuration = configuration;
  }

  /// <summary>
  /// Executes the heartbeat job to maintain consumer agent registration.
  /// Sends heartbeat to producer service with health data and consumer group assignments.
  /// </summary>
  /// <param name="context">Quartz job execution context</param>
  /// <returns>Task representing the async heartbeat operation</returns>
  public async Task Execute(IJobExecutionContext context)
  {
    _logger.LogDebug("Consumer heartbeat job started");

    try
    {
      var serviceId = Environment.GetEnvironmentVariable("SERVICE_ID")
          ?? Environment.GetEnvironmentVariable("CONSUMER_SERVICE_ID")
          ?? $"consumer-{Environment.MachineName}";

      var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
          ?? $"{serviceId}-{Guid.NewGuid():N}";

      var producerServiceUrl = _configuration["ProducerService:BaseUrl"] ?? "http://localhost:5299";

      using var httpClient = new HttpClient();

      var heartbeatRequest = new
      {
        ServiceId = serviceId,
        InstanceId = instanceId,
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
        _logger.LogDebug("Heartbeat sent successfully for Consumer Service {ServiceId}", serviceId);
      }
      else
      {
        _logger.LogWarning("Failed to send heartbeat for Consumer Service {ServiceId}. Status: {Status}",
            serviceId, response.StatusCode);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in ConsumerHeartbeatJob");
    }

    _logger.LogDebug("Consumer heartbeat job completed");
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
