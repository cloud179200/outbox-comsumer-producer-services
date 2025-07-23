using Quartz;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Jobs;

/// <summary>
/// Scheduled job that maintains producer agent registration and performs system health monitoring.
/// Sends periodic heartbeats, collects health data, and manages agent lifecycle operations.
/// Prevents concurrent execution to avoid resource conflicts and duplicate operations.
/// </summary>
[DisallowConcurrentExecution]
public class AgentHeartbeatJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<AgentHeartbeatJob> _logger;
  private readonly IConfiguration _configuration;
  private readonly string _serviceId;
  private readonly string _instanceId;

  /// <summary>
  /// Initializes the Agent Heartbeat Job with required dependencies and service identification.
  /// Extracts service ID and instance ID from environment variables for agent tracking.
  /// </summary>
  /// <param name="serviceProvider">Service provider for dependency resolution</param>
  /// <param name="logger">Logger for tracking heartbeat operations</param>
  /// <param name="configuration">Configuration for service settings</param>
  /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing</exception>
  public AgentHeartbeatJob(
      IServiceProvider serviceProvider,
      ILogger<AgentHeartbeatJob> logger,
      IConfiguration configuration)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _configuration = configuration;

    _serviceId = Environment.GetEnvironmentVariable("SERVICE_ID")
        ?? Environment.GetEnvironmentVariable("PRODUCER_SERVICE_ID")
        ?? throw new InvalidOperationException("SERVICE_ID or PRODUCER_SERVICE_ID environment variable must be set");
    _instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
        ?? throw new InvalidOperationException("INSTANCE_ID environment variable must be set");
  }

  /// <summary>
  /// Executes the heartbeat job to maintain agent registration and perform system maintenance.
  /// Sends heartbeat with health data, performs health checks on other agents, and cleans up inactive agents.
  /// </summary>
  /// <param name="context">Quartz job execution context</param>
  /// <returns>Task representing the async heartbeat operation</returns>
  public async Task Execute(IJobExecutionContext context)
  {
    try
    {
      using var scope = _serviceProvider.CreateScope();
      var agentService = scope.ServiceProvider.GetRequiredService<IAgentService>();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

      // Send heartbeat
      var heartbeatRequest = new AgentHeartbeatRequest
      {
        ServiceId = _serviceId,
        InstanceId = _instanceId,
        Status = AgentStatus.Active,
        HealthStatus = HealthStatus.Healthy,
        StatusMessage = "Producer service running normally",
        HealthData = await CollectHealthData(outboxService)
      };

      await agentService.UpdateProducerHeartbeatAsync(heartbeatRequest);

      // Perform health check on other agents
      await agentService.PerformHealthCheckAsync(_serviceId, ServiceType.Producer);

      // Cleanup inactive agents
      await agentService.CleanupInactiveAgentsAsync();

      _logger.LogDebug("Heartbeat sent for Producer Service {ServiceId}", _serviceId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending heartbeat for Producer Service {ServiceId}", _serviceId);
    }
  }

  private async Task<Dictionary<string, object>> CollectHealthData(IOutboxService outboxService)
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

      // Get outbox metrics
      try
      {
        var pendingMessages = await outboxService.GetPendingMessagesAsync(1);
        healthData["pendingMessagesCount"] = pendingMessages.Count;
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Could not collect outbox metrics for health data");
        healthData["pendingMessagesCount"] = -1;
      }

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
