namespace ProducerService.Models.DTOs;

// Request/Response DTOs
public class MessageRequest
{
  public string Topic { get; set; } = string.Empty;
  public string Message { get; set; } = string.Empty;
  public string? ConsumerGroup { get; set; } // Optional - if not provided, will send to all registered consumer groups for the topic
  public bool UseBatching { get; set; } = true; // If true, queue for batch processing; if false, process immediately
}

public class MessageResponse
{
  public string MessageId { get; set; } = string.Empty;
  public string Status { get; set; } = string.Empty;
  public DateTime Timestamp { get; set; } = DateTime.UtcNow;
  public string Topic { get; set; } = string.Empty;
  public List<string> TargetConsumerGroups { get; set; } = new();
  public string ProducerServiceId { get; set; } = string.Empty;
  public string ProducerInstanceId { get; set; } = string.Empty;
}

public class AcknowledgmentRequest
{
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public bool Success { get; set; } = true;
  public string? ErrorMessage { get; set; }
}
