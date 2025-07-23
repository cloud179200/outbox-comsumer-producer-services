namespace ConsumerService.Models.Enums;

/// <summary>
/// Defines the type of service in the distributed system.
/// Used for service discovery and health monitoring.
/// </summary>
public enum ServiceType
{
  /// <summary>
  /// Service that produces messages to the outbox
  /// </summary>
  Producer,

  /// <summary>
  /// Service that consumes messages from topics
  /// </summary>
  Consumer
}
