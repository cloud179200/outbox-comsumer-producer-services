namespace ProducerService.Models.Core;

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
  public int TopicRegistrationId { get; set; }

  // Producer Service identification for horizontal scaling
  public string ProducerServiceId { get; set; } = string.Empty;
  public string ProducerInstanceId { get; set; } = string.Empty;

  // Targeted retry properties
  public bool IsRetry { get; set; } = false;
  public string? TargetConsumerServiceId { get; set; }
  public string? OriginalMessageId { get; set; }
  public DateTime? ScheduledRetryAt { get; set; }

  // Idempotency key for preventing duplicate processing
  public string IdempotencyKey { get; set; } = string.Empty;

  // Navigation property
  public TopicRegistration? TopicRegistration { get; set; }
}

public enum OutboxMessageStatus
{
  Pending,
  Sent,
  Acknowledged,
  Failed,
  Expired
}
