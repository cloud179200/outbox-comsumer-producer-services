namespace ProducerService.Models.Enums;

/// <summary>
/// Represents the current status of an outbox message in the processing pipeline.
/// Used to track message lifecycle from creation to final acknowledgment or failure.
/// </summary>
public enum OutboxMessageStatus
{
  /// <summary>
  /// Message has been created and is waiting to be sent to Kafka.
  /// </summary>
  Pending,

  /// <summary>
  /// Message has been successfully sent to Kafka but not yet acknowledged by consumers.
  /// </summary>
  Sent,

  /// <summary>
  /// Message has been processed successfully and acknowledged by the consumer.
  /// </summary>
  Acknowledged,

  /// <summary>
  /// Message processing failed and cannot be retried further.
  /// </summary>
  Failed,

  /// <summary>
  /// Message has exceeded the maximum retry timeout and is considered expired.
  /// </summary>
  Expired
}
