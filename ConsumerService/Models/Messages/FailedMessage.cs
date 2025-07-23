namespace ConsumerService.Models.Messages;

/// <summary>
/// Represents a message that failed to be processed by a consumer.
/// Used for tracking failures, debugging, and retry mechanisms.
/// </summary>
public class FailedMessage
{
  /// <summary>
  /// Auto-incrementing database identifier
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Unique identifier of the failed message
  /// </summary>
  public string MessageId { get; set; } = string.Empty;

  /// <summary>
  /// The consumer group that failed to process this message
  /// </summary>
  public string ConsumerGroup { get; set; } = string.Empty;

  /// <summary>
  /// The topic this message failed to process from
  /// </summary>
  public string Topic { get; set; } = string.Empty;

  /// <summary>
  /// Detailed error message describing why processing failed (optional)
  /// </summary>
  public string? ErrorMessage { get; set; }

  /// <summary>
  /// When the message processing failed
  /// </summary>
  public DateTime FailedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Number of retry attempts made before marking as failed
  /// </summary>
  public int RetryCount { get; set; } = 0;

  /// <summary>
  /// The original message content that failed to process (optional)
  /// </summary>
  public string? Content { get; set; }

  /// <summary>
  /// Identifier of the producer service that created this message
  /// </summary>
  public string ProducerServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Identifier of the specific producer instance that created this message
  /// </summary>
  public string ProducerInstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Identifier of the consumer service that failed to process this message
  /// </summary>
  public string ConsumerServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Identifier of the specific consumer instance that failed to process this message
  /// </summary>
  public string ConsumerInstanceId { get; set; } = string.Empty;
}
