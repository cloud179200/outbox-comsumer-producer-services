using Microsoft.EntityFrameworkCore;
using ProducerService.Data;
using ProducerService.Models;
using System.Net;

namespace ProducerService.Services;

public interface IAgentService
{
  // Producer Agent Management
  Task<AgentResponse> RegisterProducerAgentAsync(AgentRegistrationRequest request);
  Task<bool> UpdateProducerHeartbeatAsync(AgentHeartbeatRequest request);
  Task<List<AgentResponse>> GetActiveProducerAgentsAsync();
  Task<AgentResponse?> GetProducerAgentAsync(string serviceId);
  Task<bool> DeactivateProducerAgentAsync(string serviceId);

  // Consumer Agent Management
  Task<AgentResponse> RegisterConsumerAgentAsync(AgentRegistrationRequest request);
  Task<bool> UpdateConsumerHeartbeatAsync(AgentHeartbeatRequest request);
  Task<List<AgentResponse>> GetActiveConsumerAgentsAsync();
  Task<AgentResponse?> GetConsumerAgentAsync(string serviceId);
  Task<bool> DeactivateConsumerAgentAsync(string serviceId);

  // Service Discovery
  Task<List<AgentResponse>> DiscoverServicesAsync(ServiceType? serviceType = null);
  Task<AgentResponse?> GetHealthiestProducerAsync();
  Task<List<AgentResponse>> GetHealthyConsumersForGroupAsync(string consumerGroup);

  // Health Monitoring
  Task PerformHealthCheckAsync(string serviceId, ServiceType serviceType);
  Task CleanupInactiveAgentsAsync();

  // Load Balancing
  Task<AgentResponse?> GetLeastLoadedProducerAsync();
  Task<AgentResponse?> GetBestConsumerForTopicAsync(string topic);
}

public class AgentService : IAgentService
{
  private readonly OutboxDbContext _dbContext;
  private readonly ILogger<AgentService> _logger;
  private readonly IConfiguration _configuration;
  private readonly HttpClient _httpClient;
  private readonly string _currentServiceId;
  private readonly string _currentInstanceId;

  /// <summary>
  /// Initializes a new instance of the AgentService with required dependencies.
  /// Sets up service identification from environment variables for agent tracking.
  /// </summary>
  /// <param name="dbContext">Database context for agent operations</param>
  /// <param name="logger">Logger for tracking agent operations</param>
  /// <param name="configuration">Configuration for service settings</param>
  /// <param name="httpClient">HTTP client for health check operations</param>
  public AgentService(
      OutboxDbContext dbContext,
      ILogger<AgentService> logger,
      IConfiguration configuration,
      HttpClient httpClient)
  {
    _dbContext = dbContext;
    _logger = logger;
    _configuration = configuration;
    _httpClient = httpClient;

    // Get current service identification from environment
    _currentServiceId = Environment.GetEnvironmentVariable("SERVICE_ID")
        ?? Environment.GetEnvironmentVariable("PRODUCER_SERVICE_ID")
        ?? $"producer-{Environment.MachineName}";
    _currentInstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
        ?? $"{_currentServiceId}-{Guid.NewGuid():N}";
  }

  /// <summary>
  /// Registers a new producer agent in the system for service discovery and load balancing.
  /// Creates or updates agent registration with current status and metadata.
  /// </summary>
  /// <param name="request">Registration details including service identification and endpoints</param>
  /// <returns>Agent response with registration details and assigned ID</returns>
  public async Task<AgentResponse> RegisterProducerAgentAsync(AgentRegistrationRequest request)
  {
    try
    {
      var existingAgent = await _dbContext.ProducerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == request.ServiceId);

      ProducerServiceAgent agent;

      if (existingAgent != null)
      {
        // Update existing agent
        existingAgent.ServiceName = request.ServiceName;
        existingAgent.HostName = request.HostName;
        existingAgent.IpAddress = request.IpAddress;
        existingAgent.Port = request.Port;
        existingAgent.BaseUrl = request.BaseUrl;
        existingAgent.Status = AgentStatus.Active;
        existingAgent.LastHeartbeat = DateTime.UtcNow;
        existingAgent.Version = request.Version;
        existingAgent.Metadata = request.Metadata;
        existingAgent.InstanceId = Guid.NewGuid().ToString();

        agent = existingAgent;
      }
      else
      {
        // Register new agent
        agent = new ProducerServiceAgent
        {
          ServiceId = request.ServiceId,
          InstanceId = Guid.NewGuid().ToString(),
          ServiceName = request.ServiceName,
          HostName = request.HostName,
          IpAddress = request.IpAddress,
          Port = request.Port,
          BaseUrl = request.BaseUrl,
          Status = AgentStatus.Active,
          StartedAt = DateTime.UtcNow,
          LastHeartbeat = DateTime.UtcNow,
          Version = request.Version,
          Metadata = request.Metadata
        };

        _dbContext.ProducerServiceAgents.Add(agent);
      }

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Producer agent {ServiceId} registered successfully", request.ServiceId);

      return new AgentResponse
      {
        Id = agent.Id,
        ServiceId = agent.ServiceId,
        InstanceId = agent.InstanceId,
        ServiceName = agent.ServiceName,
        BaseUrl = agent.BaseUrl,
        Status = agent.Status,
        StartedAt = agent.StartedAt,
        LastHeartbeat = agent.LastHeartbeat,
        Version = agent.Version,
        ServiceType = ServiceType.Producer
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error registering producer agent {ServiceId}", request.ServiceId);
      throw;
    }
  }

  /// <summary>
  /// Registers a new consumer agent with assigned consumer groups and topics.
  /// Enables targeted message delivery and horizontal scaling across consumer instances.
  /// </summary>
  /// <param name="request">Registration details including consumer group assignments</param>
  /// <returns>Agent response with registration details and assigned configurations</returns>
  public async Task<AgentResponse> RegisterConsumerAgentAsync(AgentRegistrationRequest request)
  {
    try
    {
      var existingAgent = await _dbContext.ConsumerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == request.ServiceId);

      ConsumerServiceAgent agent;

      if (existingAgent != null)
      {
        // Update existing agent
        existingAgent.ServiceName = request.ServiceName;
        existingAgent.HostName = request.HostName;
        existingAgent.IpAddress = request.IpAddress;
        existingAgent.Port = request.Port;
        existingAgent.BaseUrl = request.BaseUrl;
        existingAgent.Status = AgentStatus.Active;
        existingAgent.LastHeartbeat = DateTime.UtcNow;
        existingAgent.AssignedConsumerGroups = request.AssignedConsumerGroups;
        existingAgent.AssignedTopics = request.AssignedTopics;
        existingAgent.Version = request.Version;
        existingAgent.Metadata = request.Metadata;
        existingAgent.InstanceId = Guid.NewGuid().ToString();

        agent = existingAgent;
      }
      else
      {
        // Register new agent
        agent = new ConsumerServiceAgent
        {
          ServiceId = request.ServiceId,
          InstanceId = Guid.NewGuid().ToString(),
          ServiceName = request.ServiceName,
          HostName = request.HostName,
          IpAddress = request.IpAddress,
          Port = request.Port,
          BaseUrl = request.BaseUrl,
          Status = AgentStatus.Active,
          StartedAt = DateTime.UtcNow,
          LastHeartbeat = DateTime.UtcNow,
          AssignedConsumerGroups = request.AssignedConsumerGroups,
          AssignedTopics = request.AssignedTopics,
          Version = request.Version,
          Metadata = request.Metadata
        };

        _dbContext.ConsumerServiceAgents.Add(agent);
      }

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Consumer agent {ServiceId} registered successfully", request.ServiceId); return new AgentResponse
      {
        Id = agent.Id,
        ServiceId = agent.ServiceId,
        InstanceId = agent.InstanceId,
        ServiceName = agent.ServiceName,
        BaseUrl = agent.BaseUrl,
        Status = agent.Status,
        StartedAt = agent.StartedAt,
        LastHeartbeat = agent.LastHeartbeat,
        Version = agent.Version,
        ServiceType = ServiceType.Consumer,
        AssignedConsumerGroups = agent.AssignedConsumerGroups,
        AssignedTopics = agent.AssignedTopics
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error registering consumer agent {ServiceId}", request.ServiceId);
      throw;
    }
  }

  /// <summary>
  /// Updates producer agent heartbeat to maintain active status and health information.
  /// Records health metrics and system status for monitoring and load balancing decisions.
  /// </summary>
  /// <param name="request">Heartbeat information including status and health data</param>
  /// <returns>True if heartbeat successfully recorded, false if agent not found</returns>
  /// <summary>
  /// Updates producer agent heartbeat to maintain active status and health monitoring.
  /// Records health data and status updates for load balancing decisions.
  /// </summary>
  /// <param name="request">Heartbeat request containing status and health information</param>
  /// <returns>True if heartbeat was successfully updated, false if agent not found</returns>
  public async Task<bool> UpdateProducerHeartbeatAsync(AgentHeartbeatRequest request)
  {
    try
    {
      var agent = await _dbContext.ProducerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == request.ServiceId);

      if (agent == null)
      {
        _logger.LogWarning("Producer agent {ServiceId} not found for heartbeat update", request.ServiceId);
        return false;
      }

      agent.LastHeartbeat = DateTime.UtcNow;
      agent.Status = request.Status;

      // Record health check
      var healthCheck = new ServiceHealthCheck
      {
        ServiceId = request.ServiceId,
        InstanceId = request.InstanceId,
        ServiceType = ServiceType.Producer,
        Status = request.HealthStatus,
        CheckedAt = DateTime.UtcNow,
        StatusMessage = request.StatusMessage,
        HealthData = request.HealthData
      };

      _dbContext.ServiceHealthChecks.Add(healthCheck);
      await _dbContext.SaveChangesAsync();

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating producer heartbeat for {ServiceId}", request.ServiceId);
      return false;
    }
  }

  /// <summary>
  /// Updates consumer agent heartbeat with current processing status and assigned workload.
  /// Maintains consumer group assignments and processing metrics for workload distribution.
  /// </summary>
  /// <param name="request">Heartbeat information including consumer group status</param>
  /// <returns>True if heartbeat successfully recorded, false if agent not found</returns>
  /// <summary>
  /// Updates consumer agent heartbeat to maintain active status and track assigned consumer groups.
  /// Records health checks and status updates for service discovery and monitoring.
  /// </summary>
  /// <param name="request">Heartbeat request with status, health data, and consumer group assignments</param>
  /// <returns>True if heartbeat was successfully updated, false if agent not found</returns>
  public async Task<bool> UpdateConsumerHeartbeatAsync(AgentHeartbeatRequest request)
  {
    try
    {
      var agent = await _dbContext.ConsumerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == request.ServiceId);

      if (agent == null)
      {
        _logger.LogWarning("Consumer agent {ServiceId} not found for heartbeat update", request.ServiceId);
        return false;
      }

      agent.LastHeartbeat = DateTime.UtcNow;
      agent.Status = request.Status;

      // Record health check
      var healthCheck = new ServiceHealthCheck
      {
        ServiceId = request.ServiceId,
        InstanceId = request.InstanceId,
        ServiceType = ServiceType.Consumer,
        Status = request.HealthStatus,
        CheckedAt = DateTime.UtcNow,
        StatusMessage = request.StatusMessage,
        HealthData = request.HealthData
      };

      _dbContext.ServiceHealthChecks.Add(healthCheck);
      await _dbContext.SaveChangesAsync();

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating consumer heartbeat for {ServiceId}", request.ServiceId);
      return false;
    }
  }

  /// <summary>
  /// Retrieves all currently active producer agents for service discovery and load balancing.
  /// Returns agents that have sent recent heartbeats and are available for message processing.
  /// </summary>
  /// <returns>List of active producer agents with their current status and endpoints</returns>
  public async Task<List<AgentResponse>> GetActiveProducerAgentsAsync()
  {
    try
    {
      var agents = await _dbContext.ProducerServiceAgents
          .Where(a => a.Status == AgentStatus.Active)
          .OrderBy(a => a.ServiceId)
          .ToListAsync();

      return agents.Select(a => new AgentResponse
      {
        Id = a.Id,
        ServiceId = a.ServiceId,
        InstanceId = a.InstanceId,
        ServiceName = a.ServiceName,
        BaseUrl = a.BaseUrl,
        Status = a.Status,
        StartedAt = a.StartedAt,
        LastHeartbeat = a.LastHeartbeat,
        Version = a.Version,
        ServiceType = ServiceType.Producer
      }).ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting active producer agents");
      return new List<AgentResponse>();
    }
  }

  /// <summary>
  /// Retrieves all currently active consumer agents with their assigned consumer groups.
  /// Essential for targeted message delivery and understanding system capacity.
  /// </summary>
  /// <returns>List of active consumer agents with group assignments and processing status</returns>
  public async Task<List<AgentResponse>> GetActiveConsumerAgentsAsync()
  {
    try
    {
      var agents = await _dbContext.ConsumerServiceAgents
          .Where(a => a.Status == AgentStatus.Active)
          .OrderBy(a => a.ServiceId)
          .ToListAsync(); return agents.Select(a => new AgentResponse
          {
            Id = a.Id,
            ServiceId = a.ServiceId,
            InstanceId = a.InstanceId,
            ServiceName = a.ServiceName,
            BaseUrl = a.BaseUrl,
            Status = a.Status,
            StartedAt = a.StartedAt,
            LastHeartbeat = a.LastHeartbeat,
            Version = a.Version,
            ServiceType = ServiceType.Consumer,
            AssignedConsumerGroups = a.AssignedConsumerGroups,
            AssignedTopics = a.AssignedTopics
          }).ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting active consumer agents");
      return new List<AgentResponse>();
    }
  }

  public async Task<AgentResponse?> GetProducerAgentAsync(string serviceId)
  {
    try
    {
      var agent = await _dbContext.ProducerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == serviceId);

      if (agent == null) return null;

      return new AgentResponse
      {
        Id = agent.Id,
        ServiceId = agent.ServiceId,
        InstanceId = agent.InstanceId,
        ServiceName = agent.ServiceName,
        BaseUrl = agent.BaseUrl,
        Status = agent.Status,
        StartedAt = agent.StartedAt,
        LastHeartbeat = agent.LastHeartbeat,
        Version = agent.Version,
        ServiceType = ServiceType.Producer
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting producer agent {ServiceId}", serviceId);
      return null;
    }
  }

  public async Task<AgentResponse?> GetConsumerAgentAsync(string serviceId)
  {
    try
    {
      var agent = await _dbContext.ConsumerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == serviceId); if (agent == null) return null;

      return new AgentResponse
      {
        Id = agent.Id,
        ServiceId = agent.ServiceId,
        InstanceId = agent.InstanceId,
        ServiceName = agent.ServiceName,
        BaseUrl = agent.BaseUrl,
        Status = agent.Status,
        StartedAt = agent.StartedAt,
        LastHeartbeat = agent.LastHeartbeat,
        Version = agent.Version,
        ServiceType = ServiceType.Consumer,
        AssignedConsumerGroups = agent.AssignedConsumerGroups,
        AssignedTopics = agent.AssignedTopics
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting consumer agent {ServiceId}", serviceId);
      return null;
    }
  }

  public async Task<bool> DeactivateProducerAgentAsync(string serviceId)
  {
    try
    {
      var agent = await _dbContext.ProducerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == serviceId);

      if (agent == null) return false;

      agent.Status = AgentStatus.Inactive;
      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Producer agent {ServiceId} deactivated", serviceId);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating producer agent {ServiceId}", serviceId);
      return false;
    }
  }

  public async Task<bool> DeactivateConsumerAgentAsync(string serviceId)
  {
    try
    {
      var agent = await _dbContext.ConsumerServiceAgents
          .FirstOrDefaultAsync(a => a.ServiceId == serviceId);

      if (agent == null) return false;

      agent.Status = AgentStatus.Inactive;
      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Consumer agent {ServiceId} deactivated", serviceId);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating consumer agent {ServiceId}", serviceId);
      return false;
    }
  }

  public async Task<List<AgentResponse>> DiscoverServicesAsync(ServiceType? serviceType = null)
  {
    var allAgents = new List<AgentResponse>();

    if (serviceType == null || serviceType == ServiceType.Producer)
    {
      var producers = await GetActiveProducerAgentsAsync();
      allAgents.AddRange(producers);
    }

    if (serviceType == null || serviceType == ServiceType.Consumer)
    {
      var consumers = await GetActiveConsumerAgentsAsync();
      allAgents.AddRange(consumers);
    }

    return allAgents;
  }

  public async Task<AgentResponse?> GetHealthiestProducerAsync()
  {
    try
    {
      var producers = await _dbContext.ProducerServiceAgents
          .Where(a => a.Status == AgentStatus.Active)
          .ToListAsync();

      if (!producers.Any()) return null;

      // For now, return the one with the most recent heartbeat
      // In a more sophisticated implementation, you could consider CPU usage, memory, etc.
      var healthiest = producers
          .OrderByDescending(a => a.LastHeartbeat)
          .First();

      return new AgentResponse
      {
        Id = healthiest.Id,
        ServiceId = healthiest.ServiceId,
        InstanceId = healthiest.InstanceId,
        ServiceName = healthiest.ServiceName,
        BaseUrl = healthiest.BaseUrl,
        Status = healthiest.Status,
        StartedAt = healthiest.StartedAt,
        LastHeartbeat = healthiest.LastHeartbeat,
        Version = healthiest.Version,
        ServiceType = ServiceType.Producer
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting healthiest producer");
      return null;
    }
  }

  /// <summary>
  /// Finds consumer agents assigned to a specific consumer group for targeted message delivery.
  /// Enables efficient routing of messages to appropriate consumer instances.
  /// </summary>
  /// <param name="consumerGroup">The consumer group to find agents for</param>
  /// <returns>List of healthy consumer agents assigned to the specified group</returns>
  public async Task<List<AgentResponse>> GetHealthyConsumersForGroupAsync(string consumerGroup)
  {
    try
    {
      var consumers = await _dbContext.ConsumerServiceAgents
          .Where(a => a.Status == AgentStatus.Active &&
                     a.AssignedConsumerGroups.Contains(consumerGroup))
          .ToListAsync(); return consumers.Select(a => new AgentResponse
          {
            Id = a.Id,
            ServiceId = a.ServiceId,
            InstanceId = a.InstanceId,
            ServiceName = a.ServiceName,
            BaseUrl = a.BaseUrl,
            Status = a.Status,
            StartedAt = a.StartedAt,
            LastHeartbeat = a.LastHeartbeat,
            Version = a.Version,
            ServiceType = ServiceType.Consumer,
            AssignedConsumerGroups = a.AssignedConsumerGroups,
            AssignedTopics = a.AssignedTopics
          }).ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting healthy consumers for group {ConsumerGroup}", consumerGroup);
      return new List<AgentResponse>();
    }
  }

  /// <summary>
  /// Performs health checks on registered agents by calling their health endpoints.
  /// Records response times and health status for monitoring and alerting purposes.
  /// </summary>
  /// <param name="serviceId">The service ID of the agent to check</param>
  /// <param name="serviceType">The type of service (Producer or Consumer)</param>
  /// <returns>Task representing the health check operation</returns>
  public async Task PerformHealthCheckAsync(string serviceId, ServiceType serviceType)
  {
    try
    {
      AgentResponse? agent = serviceType == ServiceType.Producer
          ? await GetProducerAgentAsync(serviceId)
          : await GetConsumerAgentAsync(serviceId);

      if (agent == null) return;

      var healthEndpoint = $"{agent.BaseUrl.TrimEnd('/')}/api/{(serviceType == ServiceType.Producer ? "messages" : "consumer")}/health";

      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var healthStatus = HealthStatus.Unknown;
      string? statusMessage = null;

      try
      {
        var response = await _httpClient.GetAsync(healthEndpoint);
        stopwatch.Stop();

        healthStatus = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        statusMessage = response.IsSuccessStatusCode ? "OK" : $"HTTP {response.StatusCode}";
      }
      catch (Exception ex)
      {
        stopwatch.Stop();
        healthStatus = HealthStatus.Unhealthy;
        statusMessage = ex.Message;
      }

      var healthCheck = new ServiceHealthCheck
      {
        ServiceId = serviceId,
        InstanceId = agent.InstanceId,
        ServiceType = serviceType,
        Status = healthStatus,
        CheckedAt = DateTime.UtcNow,
        StatusMessage = statusMessage,
        ResponseTimeMs = stopwatch.ElapsedMilliseconds
      };

      _dbContext.ServiceHealthChecks.Add(healthCheck);
      await _dbContext.SaveChangesAsync();

    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error performing health check for {ServiceType} {ServiceId}", serviceType, serviceId);
    }
  }

  /// <summary>
  /// Removes inactive agents that haven't sent heartbeats within the configured timeout period.
  /// Maintains system accuracy by cleaning up stale agent registrations automatically.
  /// </summary>
  /// <returns>Task representing the cleanup operation</returns>
  public async Task CleanupInactiveAgentsAsync()
  {
    try
    {
      var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Consider agents inactive after 5 minutes

      // Cleanup inactive producer agents
      var inactiveProducers = await _dbContext.ProducerServiceAgents
          .Where(a => a.LastHeartbeat < cutoffTime && a.Status == AgentStatus.Active)
          .ToListAsync();

      foreach (var producer in inactiveProducers)
      {
        producer.Status = AgentStatus.Inactive;
        _logger.LogWarning("Producer agent {ServiceId} marked as inactive due to missed heartbeat", producer.ServiceId);
      }

      // Cleanup inactive consumer agents
      var inactiveConsumers = await _dbContext.ConsumerServiceAgents
          .Where(a => a.LastHeartbeat < cutoffTime && a.Status == AgentStatus.Active)
          .ToListAsync();

      foreach (var consumer in inactiveConsumers)
      {
        consumer.Status = AgentStatus.Inactive;
        _logger.LogWarning("Consumer agent {ServiceId} marked as inactive due to missed heartbeat", consumer.ServiceId);
      }

      // Cleanup old health checks (keep only last 24 hours)
      var oldHealthChecks = await _dbContext.ServiceHealthChecks
          .Where(h => h.CheckedAt < DateTime.UtcNow.AddHours(-24))
          .ToListAsync();

      _dbContext.ServiceHealthChecks.RemoveRange(oldHealthChecks);

      await _dbContext.SaveChangesAsync();

      if (inactiveProducers.Any() || inactiveConsumers.Any() || oldHealthChecks.Any())
      {
        _logger.LogInformation("Cleanup completed: {ProducerCount} producers, {ConsumerCount} consumers marked inactive, {HealthCount} old health checks removed",
            inactiveProducers.Count, inactiveConsumers.Count, oldHealthChecks.Count);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during agent cleanup");
    }
  }

  public async Task<AgentResponse?> GetLeastLoadedProducerAsync()
  {
    try
    {
      var producers = await _dbContext.ProducerServiceAgents
          .Where(a => a.Status == AgentStatus.Active)
          .Include(a => a.Messages.Where(m => m.Status == OutboxMessageStatus.Pending))
          .ToListAsync();

      if (!producers.Any()) return null;

      // Find producer with least pending messages
      var leastLoaded = producers
          .OrderBy(a => a.Messages.Count)
          .ThenByDescending(a => a.LastHeartbeat)
          .First();

      return new AgentResponse
      {
        Id = leastLoaded.Id,
        ServiceId = leastLoaded.ServiceId,
        InstanceId = leastLoaded.InstanceId,
        ServiceName = leastLoaded.ServiceName,
        BaseUrl = leastLoaded.BaseUrl,
        Status = leastLoaded.Status,
        StartedAt = leastLoaded.StartedAt,
        LastHeartbeat = leastLoaded.LastHeartbeat,
        Version = leastLoaded.Version,
        ServiceType = ServiceType.Producer
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting least loaded producer");
      return null;
    }
  }

  public async Task<AgentResponse?> GetBestConsumerForTopicAsync(string topic)
  {
    try
    {
      var consumers = await _dbContext.ConsumerServiceAgents
          .Where(a => a.Status == AgentStatus.Active &&
                     a.AssignedTopics.Contains(topic))
          .ToListAsync();

      if (!consumers.Any()) return null;

      // For now, return the one with the most recent heartbeat
      // In production, you could consider message processing rate, queue depth, etc.
      var bestConsumer = consumers
          .OrderByDescending(a => a.LastHeartbeat)
          .First(); return new AgentResponse
          {
            Id = bestConsumer.Id,
            ServiceId = bestConsumer.ServiceId,
            InstanceId = bestConsumer.InstanceId,
            ServiceName = bestConsumer.ServiceName,
            BaseUrl = bestConsumer.BaseUrl,
            Status = bestConsumer.Status,
            StartedAt = bestConsumer.StartedAt,
            LastHeartbeat = bestConsumer.LastHeartbeat,
            Version = bestConsumer.Version,
            ServiceType = ServiceType.Consumer,
            AssignedConsumerGroups = bestConsumer.AssignedConsumerGroups,
            AssignedTopics = bestConsumer.AssignedTopics
          };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting best consumer for topic {Topic}", topic);
      return null;
    }
  }

  public string GetCurrentServiceId() => _currentServiceId;
  public string GetCurrentInstanceId() => _currentInstanceId;
}
