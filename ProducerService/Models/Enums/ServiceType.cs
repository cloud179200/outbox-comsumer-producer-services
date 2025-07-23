namespace ProducerService.Models.Enums;

/// <summary>
/// Identifies the type of service in the outbox pattern system.
/// Used for service discovery and routing decisions.
/// </summary>
public enum ServiceType
{
  /// <summary>
  /// Service that creates and publishes messages to the outbox.
  /// </summary>
  Producer,

  /// <summary>
  /// Service that consumes and processes messages from topics.
  /// </summary>
  Consumer
}
