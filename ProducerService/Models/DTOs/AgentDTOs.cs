using ProducerService.Models.Enums;

namespace ProducerService.Models.DTOs;

/// <summary>
/// Request model for registering a new service agent in the system.
/// Contains all necessary information for service discovery and management.
/// </summary>
public class AgentRegistrationRequest
{
  /// <summary>
  /// Unique identifier for this service instance.
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

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
  /// Full base URL for API communication.
  /// </summary>
  public string BaseUrl { get; set; } = string.Empty;

  /// <summary>
  /// Type of service being registered (Producer or Consumer).
  /// </summary>
  public ServiceType ServiceType { get; set; }

  /// <summary>
  /// Version information for this service.
  /// </summary>
  public string? Version { get; set; }

  /// <summary>
  /// Additional metadata for service configuration.
  /// </summary>
  public Dictionary<string, string> Metadata { get; set; } = new();

  /// <summary>
  /// Consumer groups this service is assigned to process (Consumer services only).
  /// </summary>
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();

  /// <summary>
  /// Topics this service is configured to handle.
  /// </summary>
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Request model for service heartbeat updates.
/// Used to maintain service health and operational status.
/// </summary>
public class AgentHeartbeatRequest
{
  /// <summary>
  /// Service identifier sending the heartbeat.
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Instance identifier sending the heartbeat.
  /// </summary>
  public string InstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Current operational status of the service.
  /// </summary>
  public AgentStatus Status { get; set; } = AgentStatus.Active;

  /// <summary>
  /// Current health status based on internal checks.
  /// </summary>
  public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;

  /// <summary>
  /// Optional status message or error details.
  /// </summary>
  public string? StatusMessage { get; set; }

  /// <summary>
  /// Additional health metrics and diagnostic information.
  /// </summary>
  public Dictionary<string, object> HealthData { get; set; } = new();
}

/// <summary>
/// Response model containing service agent information.
/// Used for service discovery and monitoring operations.
/// </summary>
public class AgentResponse
{
  /// <summary>
  /// Database identifier for this agent.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Unique service identifier.
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Unique instance identifier.
  /// </summary>
  public string InstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Human-readable service name.
  /// </summary>
  public string ServiceName { get; set; } = string.Empty;

  /// <summary>
  /// Base URL for API communication.
  /// </summary>
  public string BaseUrl { get; set; } = string.Empty;

  /// <summary>
  /// Current operational status.
  /// </summary>
  public AgentStatus Status { get; set; }

  /// <summary>
  /// Timestamp when service was first registered.
  /// </summary>
  public DateTime StartedAt { get; set; }

  /// <summary>
  /// Timestamp of most recent heartbeat.
  /// </summary>
  public DateTime LastHeartbeat { get; set; }

  /// <summary>
  /// Service version information.
  /// </summary>
  public string? Version { get; set; }

  /// <summary>
  /// Service type (Producer or Consumer).
  /// </summary>
  public ServiceType ServiceType { get; set; }

  /// <summary>
  /// Assigned consumer groups (for Consumer services).
  /// </summary>
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();

  /// <summary>
  /// Assigned topics for processing.
  /// </summary>
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();
}
