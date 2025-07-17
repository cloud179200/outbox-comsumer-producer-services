namespace ConsumerService.Models;

public class OutboxMessage
{
  public string Id { get; set; } = Guid.NewGuid().ToString();
  public string Topic { get; set; } = string.Empty;
  public string Message { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
  public DateTime? ProcessedAt { get; set; }
  public int RetryCount { get; set; } = 0;
  public DateTime? LastRetryAt { get; set; }
  public string? ErrorMessage { get; set; }
  public string ConsumerGroup { get; set; } = string.Empty;
}

public enum OutboxMessageStatus
{
  Pending,
  Sent,
  Acknowledged,
  Failed,
  Expired
}

public class AcknowledgmentRequest
{
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public bool Success { get; set; } = true;
  public string? ErrorMessage { get; set; }
}

public class ConsumerMessage
{
  public string MessageId { get; set; } = string.Empty;
  public string Topic { get; set; } = string.Empty;
  public string Content { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class ProcessedMessage
{
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public string Topic { get; set; } = string.Empty;
  public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
  public string? Content { get; set; }
}

public class FailedMessage
{
  public int Id { get; set; }
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public string Topic { get; set; } = string.Empty;
  public string? ErrorMessage { get; set; }
  public DateTime FailedAt { get; set; } = DateTime.UtcNow;
  public int RetryCount { get; set; } = 0;
  public string? Content { get; set; }
}
