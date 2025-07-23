using ConsumerService.Models.Enums;

namespace ConsumerService.Models.DTOs;

/// <summary>
/// Request object for registering a service agent in the distributed system.
/// Contains all necessary information for service discovery and load balancing.
/// </summary>
public class AgentRegistrationRequest
{
  /// <summary>
  /// Unique identifier for the service instance
  /// </summary>
  public string ServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Human-readable name of the service
  /// </summary>
  public string ServiceName { get; set; } = string.Empty;

  /// <summary>
  /// Hostname or machine name where the service is running
  /// </summary>
  public string HostName { get; set; } = string.Empty;

  /// <summary>
  /// IP address of the service
  /// </summary>
  public string IpAddress { get; set; } = string.Empty;

  /// <summary>
  /// Port number the service is listening on
  /// </summary>
  public int Port { get; set; }

  /// <summary>
  /// Complete base URL for accessing the service
  /// </summary>
  public string BaseUrl { get; set; } = string.Empty;

  /// <summary>
  /// Type of service (Producer or Consumer)
  /// </summary>
  public ServiceType ServiceType { get; set; }

  /// <summary>
  /// Version of the service (optional)
  /// </summary>
  public string? Version { get; set; }

  /// <summary>
  /// Additional metadata about the service
  /// </summary>
  public Dictionary<string, string> Metadata { get; set; } = new();

  /// <summary>
  /// Consumer groups this service is assigned to (for Consumer services only)
  /// </summary>
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();

  /// <summary>
  /// Topics this service handles (for Consumer services only)
  /// </summary>
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();
}
