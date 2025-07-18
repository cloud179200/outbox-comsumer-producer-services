using Quartz;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Jobs;

[DisallowConcurrentExecution]
public class AgentHeartbeatJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<AgentHeartbeatJob> _logger;
  private readonly IConfiguration _configuration;
  private readonly string _serviceId;
  private readonly string _instanceId;

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
        ?? $"producer-{Environment.MachineName}";
    _instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
        ?? $"{_serviceId}-{Guid.NewGuid():N}";
  }

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
