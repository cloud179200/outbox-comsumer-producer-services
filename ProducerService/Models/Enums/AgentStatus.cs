namespace ProducerService.Models.Enums;

/// <summary>
/// Represents the operational status of a service agent in the distributed system.
/// Used for tracking service instance health and availability.
/// </summary>
public enum AgentStatus
{
  /// <summary>
  /// Service is running normally and accepting requests.
  /// </summary>
  Active,

  /// <summary>
  /// Service is not currently processing requests but may resume.
  /// </summary>
  Inactive,

  /// <summary>
  /// Service is experiencing issues but still attempting to operate.
  /// </summary>
  Unhealthy,

  /// <summary>
  /// Service is temporarily unavailable for maintenance.
  /// </summary>
  Maintenance,

  /// <summary>
  /// Service has been permanently shut down.
  /// </summary>
  Terminated
}
