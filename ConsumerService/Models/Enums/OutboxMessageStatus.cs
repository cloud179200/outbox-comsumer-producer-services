namespace ConsumerService.Models.Enums;

/// <summary>
/// Represents the current status of an outbox message in the processing pipeline.
/// Used to track message lifecycle from creation to final acknowledgment or failure.
/// </summary>
public enum OutboxMessageStatus
{
  /// <summary>
  /// Message has been created and is waiting to be processed
  /// </summary>
  Pending,

  /// <summary>
  /// Message has been sent to the message broker (Kafka)
  /// </summary>
  Sent,

  /// <summary>
  /// Consumer has acknowledged successful processing of the message
  /// </summary>
  Acknowledged,

  /// <summary>
  /// Message processing failed after retry attempts
  /// </summary>
  Failed,

  /// <summary>
  /// Message has exceeded its maximum retention time
  /// </summary>
  Expired
}
