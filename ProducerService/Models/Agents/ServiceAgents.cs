using ProducerService.Models.Enums;

namespace ProducerService.Models.Agents;

/// <summary>
/// Represents a producer service instance in the distributed outbox system.
/// Tracks service registration, health, and message production capabilities.
/// </summary>
public class ProducerServiceAgent
{
  /// <summary>
  /// Database primary key identifier.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Unique service identifier for this producer instance.
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Unique instance identifier for horizontal scaling scenarios.
  /// </summary>
  public string InstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Human-readable name for this service.
  /// </summary>
  public string ServiceName { get; set; } = string.Empty;

  /// <summary>
  /// Host machine name where this service is running.
  /// </summary>
  public string HostName { get; set; } = string.Empty;

  /// <summary>
  /// IP address for network communication.
  /// </summary>
  public string IpAddress { get; set; } = string.Empty;

  /// <summary>
  /// Network port for HTTP communication.
  /// </summary>
  public int Port { get; set; }

  /// <summary>
  /// Full URL for API communication with this service.
  /// </summary>
  public string BaseUrl { get; set; } = string.Empty;

  /// <summary>
  /// Current operational status of this service agent.
  /// </summary>
  public AgentStatus Status { get; set; } = AgentStatus.Active;

  /// <summary>
  /// Timestamp when this service was first registered.
  /// </summary>
  public DateTime StartedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Timestamp of the most recent heartbeat from this service.
  /// </summary>
  public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Version information for this service instance.
  /// </summary>
  public string? Version { get; set; }

  /// <summary>
  /// Additional metadata associated with this service.
  /// </summary>
  public Dictionary<string, string> Metadata { get; set; } = new();

  // Navigation properties
  /// <summary>
  /// Messages produced by this service agent.
  /// </summary>
  public ICollection<Messages.OutboxMessage> Messages { get; set; } = new List<Messages.OutboxMessage>();

  /// <summary>
  /// Health check records for this service agent.
  /// </summary>
  public ICollection<ServiceHealthCheck> HealthChecks { get; set; } = new List<ServiceHealthCheck>();
}

/// <summary>
/// Represents a consumer service instance in the distributed outbox system.
/// Tracks service registration, assigned consumer groups, and processing capabilities.
/// </summary>
public class ConsumerServiceAgent
{
  /// <summary>
  /// Database primary key identifier.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Unique service identifier for this consumer instance.
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Unique instance identifier for horizontal scaling scenarios.
  /// </summary>
  public string InstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Human-readable name for this service.
  /// </summary>
  public string ServiceName { get; set; } = string.Empty;

  /// <summary>
  /// Host machine name where this service is running.
  /// </summary>
  public string HostName { get; set; } = string.Empty;

  /// <summary>
  /// IP address for network communication.
  /// </summary>
  public string IpAddress { get; set; } = string.Empty;

  /// <summary>
  /// Network port for HTTP communication.
  /// </summary>
  public int Port { get; set; }

  /// <summary>
  /// Full URL for API communication with this service.
  /// </summary>
  public string BaseUrl { get; set; } = string.Empty;

  /// <summary>
  /// Current operational status of this service agent.
  /// </summary>
  public AgentStatus Status { get; set; } = AgentStatus.Active;

  /// <summary>
  /// Timestamp when this service was first registered.
  /// </summary>
  public DateTime StartedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Timestamp of the most recent heartbeat from this service.
  /// </summary>
  public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Consumer groups assigned to this service instance.
  /// </summary>
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();

  /// <summary>
  /// Topics this consumer service is configured to process.
  /// </summary>
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();

  /// <summary>
  /// Version information for this service instance.
  /// </summary>
  public string? Version { get; set; }

  /// <summary>
  /// Additional metadata associated with this service.
  /// </summary>
  public Dictionary<string, string> Metadata { get; set; } = new();

  // Navigation properties
  /// <summary>
  /// Health check records for this service agent.
  /// </summary>
  public ICollection<ServiceHealthCheck> HealthChecks { get; set; } = new List<ServiceHealthCheck>();
}

/// <summary>
/// Records health check results for service monitoring and diagnostics.
/// Tracks service availability, performance metrics, and operational status.
/// </summary>
public class ServiceHealthCheck
{
  /// <summary>
  /// Database primary key identifier.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Service identifier this health check is for.
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Instance identifier this health check is for.
  /// </summary>
  public string InstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Type of service being health checked.
  /// </summary>
  public ServiceType ServiceType { get; set; }

  /// <summary>
  /// Result status of this health check.
  /// </summary>
  public HealthStatus Status { get; set; }

  /// <summary>
  /// Timestamp when this health check was performed.
  /// </summary>
  public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Human-readable status message or error details.
  /// </summary>
  public string? StatusMessage { get; set; }

  /// <summary>
  /// Response time in milliseconds for this health check.
  /// </summary>
  public double ResponseTimeMs { get; set; }

  /// <summary>
  /// Additional health metrics and diagnostic data.
  /// </summary>
  public Dictionary<string, object> HealthData { get; set; } = new();
}
