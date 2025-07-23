namespace ConsumerService.Models.Enums;

/// <summary>
/// Represents the health status of a service instance.
/// Used for monitoring and load balancing decisions.
/// </summary>
public enum HealthStatus
{
  /// <summary>
  /// Service is operating normally and can handle requests
  /// </summary>
  Healthy,

  /// <summary>
  /// Service is operational but with reduced performance
  /// </summary>
  Degraded,

  /// <summary>
  /// Service is not responding or failing health checks
  /// </summary>
  Unhealthy,

  /// <summary>
  /// Health status cannot be determined
  /// </summary>
  Unknown
}
