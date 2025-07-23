namespace ConsumerService.Models.Messages;

/// <summary>
/// Represents a message received by a consumer from a message broker.
/// Contains all necessary information for processing and acknowledgment.
/// </summary>
public class ConsumerMessage
{
  /// <summary>
  /// Unique identifier for the message
  /// </summary>
  public string MessageId { get; set; } = string.Empty;

  /// <summary>
  /// The topic this message was received from
  /// </summary>
  public string Topic { get; set; } = string.Empty;

  /// <summary>
  /// The actual message content/payload to be processed
  /// </summary>
  public string Content { get; set; } = string.Empty;

  /// <summary>
  /// The consumer group this message is assigned to
  /// </summary>
  public string ConsumerGroup { get; set; } = string.Empty;

  /// <summary>
  /// When this message was received by the consumer
  /// </summary>
  public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Identifier of the producer service that created this message
  /// </summary>
  public string ProducerServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Identifier of the specific producer instance that created this message
  /// </summary>
  public string ProducerInstanceId { get; set; } = string.Empty;

  // Retry and targeting properties
  /// <summary>
  /// Indicates if this message is a retry of a previously failed message
  /// </summary>
  public bool IsRetry { get; set; } = false;

  /// <summary>
  /// Specific consumer service this message is targeted to (optional)
  /// </summary>
  public string? TargetConsumerServiceId { get; set; }

  /// <summary>
  /// Reference to the original message ID if this is a retry (optional)
  /// </summary>
  public string? OriginalMessageId { get; set; }

  /// <summary>
  /// Idempotency key to prevent duplicate processing
  /// </summary>
  public string IdempotencyKey { get; set; } = string.Empty;

  /// <summary>
  /// Number of times this message has been retried
  /// </summary>
  public int RetryCount { get; set; } = 0;
}
