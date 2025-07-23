namespace ConsumerService.Models.Messages;

/// <summary>
/// Represents a message that has been successfully processed by a consumer.
/// Used for tracking and auditing successful message processing.
/// </summary>
public class ProcessedMessage
{
  /// <summary>
  /// Unique identifier of the processed message
  /// </summary>
  public string MessageId { get; set; } = string.Empty;

  /// <summary>
  /// The consumer group that processed this message
  /// </summary>
  public string ConsumerGroup { get; set; } = string.Empty;

  /// <summary>
  /// The topic this message was processed from
  /// </summary>
  public string Topic { get; set; } = string.Empty;

  /// <summary>
  /// When the message was successfully processed
  /// </summary>
  public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// The original message content that was processed (optional)
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
  /// Identifier of the consumer service that processed this message
  /// </summary>
  public string ConsumerServiceId { get; set; } = string.Empty;

  /// <summary>
  /// Identifier of the specific consumer instance that processed this message
  /// </summary>
  public string ConsumerInstanceId { get; set; } = string.Empty;

  /// <summary>
  /// Idempotency key used to prevent duplicate processing
  /// </summary>
  public string IdempotencyKey { get; set; } = string.Empty;
}
