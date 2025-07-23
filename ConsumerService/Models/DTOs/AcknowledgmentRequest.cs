namespace ConsumerService.Models.DTOs;

/// <summary>
/// Request object for acknowledging message processing status.
/// Used by consumers to report successful or failed message processing back to producers.
/// </summary>
public class AcknowledgmentRequest
{
  /// <summary>
  /// Unique identifier of the message being acknowledged
  /// </summary>
  public string MessageId { get; set; } = string.Empty;

  /// <summary>
  /// The consumer group that processed (or failed to process) the message
  /// </summary>
  public string ConsumerGroup { get; set; } = string.Empty;

  /// <summary>
  /// Indicates whether message processing was successful
  /// </summary>
  public bool Success { get; set; } = true;

  /// <summary>
  /// Error message if processing failed (optional)
  /// </summary>
  public string? ErrorMessage { get; set; }
}
