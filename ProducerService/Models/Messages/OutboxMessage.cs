using ProducerService.Models.Enums;

namespace ProducerService.Models.Messages;

/// <summary>
/// Represents a message stored in the outbox pattern for reliable delivery.
/// Contains all necessary information for message processing, retry logic, and tracking.
/// </summary>
public class OutboxMessage
{
  /// <summary>
  /// Unique identifier for this message instance.
  /// </summary>
  public string Id { get; set; } = Guid.NewGuid().ToString();

  /// <summary>
  /// The topic this message should be published to.
  /// </summary>
  public string Topic { get; set; } = string.Empty;

  /// <summary>
  /// The actual message content/payload.
  /// </summary>
  public string Message { get; set; } = string.Empty;

  /// <summary>
  /// Timestamp when this message was initially created.
  /// </summary>
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Current processing status of this message.
  /// </summary>
  public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

  /// <summary>
  /// Timestamp when the message was last processed or status updated.
  /// </summary>
  public DateTime? ProcessedAt { get; set; }

  /// <summary>
  /// Number of times this message has been retried.
  /// </summary>
  public int RetryCount { get; set; } = 0;

  /// <summary>
  /// Timestamp of the most recent retry attempt.
  /// </summary>
  public DateTime? LastRetryAt { get; set; }

  /// <summary>
  /// Error message from the last failed processing attempt.
  /// </summary>
  public string? ErrorMessage { get; set; }

  /// <summary>
  /// The consumer group this message is targeted for.
  /// </summary>
  public string ConsumerGroup { get; set; } = string.Empty;

  /// <summary>
  /// Foreign key reference to the topic registration.
  /// </summary>
  public int TopicRegistrationId { get; set; }

  /// <summary>
  /// Identifier of the producer service that created this message.
  /// Used for horizontal scaling and tracking message origins.
  /// </summary>
  public string ProducerServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Identifier of the specific producer instance that created this message.
  /// </summary>
  public string ProducerInstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Indicates if this message is a retry of a previously failed message.
  /// </summary>
  public bool IsRetry { get; set; } = false;

  /// <summary>
  /// Specific consumer service ID to target for retry scenarios.
  /// </summary>
  public string? TargetConsumerServiceId { get; set; }

  /// <summary>
  /// Reference to the original message ID if this is a retry.
  /// </summary>
  public string? OriginalMessageId { get; set; }

  /// <summary>
  /// Scheduled time for when this retry should be attempted.
  /// </summary>
  public DateTime? ScheduledRetryAt { get; set; }

  /// <summary>
  /// Unique key for preventing duplicate processing of the same logical message.
  /// </summary>
  public string IdempotencyKey { get; set; } = string.Empty;

  /// <summary>
  /// Navigation property to the associated topic registration.
  /// </summary>
  public Core.TopicRegistration? TopicRegistration { get; set; }
}
