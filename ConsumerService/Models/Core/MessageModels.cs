namespace ConsumerService.Models.Core;

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

  // Targeted retry properties
  public bool IsRetry { get; set; } = false;
  public string? TargetConsumerServiceId { get; set; }
  public string? OriginalMessageId { get; set; }
  public DateTime? ScheduledRetryAt { get; set; }

  // Idempotency key for preventing duplicate processing
  public string IdempotencyKey { get; set; } = string.Empty;
}

public enum OutboxMessageStatus
{
  Pending,
  Sent,
  Acknowledged,
  Failed,
  Expired
}

public class ConsumerMessage
{
  public string MessageId { get; set; } = string.Empty;
  public string Topic { get; set; } = string.Empty;
  public string Content { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
  public string ProducerServiceId { get; set; } = string.Empty;
  public string ProducerInstanceId { get; set; } = string.Empty;

  // Retry and targeting properties
  public bool IsRetry { get; set; } = false;
  public string? TargetConsumerServiceId { get; set; }
  public string? OriginalMessageId { get; set; }
  public string IdempotencyKey { get; set; } = string.Empty;
  public int RetryCount { get; set; } = 0;
}

public class ProcessedMessage
{
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public string Topic { get; set; } = string.Empty;
  public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
  public string? Content { get; set; }
  public string ProducerServiceId { get; set; } = string.Empty;
  public string ProducerInstanceId { get; set; } = string.Empty;
  public string ConsumerServiceId { get; set; } = string.Empty;
  public string ConsumerInstanceId { get; set; } = string.Empty;
  public string IdempotencyKey { get; set; } = string.Empty;
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
  public string ProducerServiceId { get; set; } = string.Empty;
  public string ProducerInstanceId { get; set; } = string.Empty;
  public string ConsumerServiceId { get; set; } = string.Empty;
  public string ConsumerInstanceId { get; set; } = string.Empty;
}
