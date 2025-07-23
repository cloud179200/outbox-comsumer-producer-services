using ConsumerService.Models.Enums;

namespace ConsumerService.Models.Messages;

/// <summary>
/// Represents an outbox message that ensures reliable message delivery.
/// Contains all information needed to track message lifecycle and retry logic.
/// </summary>
public class OutboxMessage
{
  /// <summary>
  /// Unique identifier for the message
  /// </summary>
  public string Id { get; set; } = Guid.NewGuid().ToString();

  /// <summary>
  /// The topic this message belongs to
  /// </summary>
  public string Topic { get; set; } = string.Empty;

  /// <summary>
  /// The actual message content/payload
  /// </summary>
  public string Message { get; set; } = string.Empty;

  /// <summary>
  /// When the message was created
  /// </summary>
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Current processing status of the message
  /// </summary>
  public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

  /// <summary>
  /// When the message was last processed (optional)
  /// </summary>
  public DateTime? ProcessedAt { get; set; }

  /// <summary>
  /// Number of retry attempts made
  /// </summary>
  public int RetryCount { get; set; } = 0;

  /// <summary>
  /// When the last retry attempt was made (optional)
  /// </summary>
  public DateTime? LastRetryAt { get; set; }

  /// <summary>
  /// Error message from failed processing (optional)
  /// </summary>
  public string? ErrorMessage { get; set; }

  /// <summary>
  /// The consumer group this message is targeted to
  /// </summary>
  public string ConsumerGroup { get; set; } = string.Empty;

  // Targeted retry properties
  /// <summary>
  /// Indicates if this message is a retry of another message
  /// </summary>
  public bool IsRetry { get; set; } = false;

  /// <summary>
  /// Specific consumer service this retry is targeted to (optional)
  /// </summary>
  public string? TargetConsumerServiceId { get; set; }

  /// <summary>
  /// Reference to the original message ID if this is a retry (optional)
  /// </summary>
  public string? OriginalMessageId { get; set; }

  /// <summary>
  /// When this retry should be attempted (optional)
  /// </summary>
  public DateTime? ScheduledRetryAt { get; set; }

  /// <summary>
  /// Idempotency key for preventing duplicate processing
  /// </summary>
  public string IdempotencyKey { get; set; } = string.Empty;
}
