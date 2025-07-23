namespace ProducerService.Models.Enums;

/// <summary>
/// Represents the health status of a service during health checks.
/// Used for monitoring and determining service availability.
/// </summary>
public enum HealthStatus
{
  /// <summary>
  /// Service is operating normally with no issues detected.
  /// </summary>
  Healthy,

  /// <summary>
  /// Service is operational but performance may be reduced.
  /// </summary>
  Degraded,

  /// <summary>
  /// Service is not functioning properly and may not respond to requests.
  /// </summary>
  Unhealthy,

  /// <summary>
  /// Health status cannot be determined due to communication or other issues.
  /// </summary>
  Unknown
}
