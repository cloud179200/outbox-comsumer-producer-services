namespace ProducerService.Models;

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

public class TopicRegistration
{
  public int Id { get; set; }
  public string TopicName { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public bool IsActive { get; set; } = true;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; set; }

  // Navigation properties
  public ICollection<ConsumerGroupRegistration> ConsumerGroups { get; set; } = new List<ConsumerGroupRegistration>();
  public ICollection<OutboxMessage> Messages { get; set; } = new List<OutboxMessage>();
}

public class ConsumerGroupRegistration
{
  public int Id { get; set; }
  public string ConsumerGroupName { get; set; } = string.Empty;
  public int TopicRegistrationId { get; set; }
  public bool RequiresAcknowledgment { get; set; } = true;
  public bool IsActive { get; set; } = true;
  public int AcknowledgmentTimeoutMinutes { get; set; } = 30;
  public int MaxRetries { get; set; } = 3;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; set; }

  // Navigation property
  public TopicRegistration TopicRegistration { get; set; } = null!;
  public ICollection<ConsumerAcknowledgment> Acknowledgments { get; set; } = new List<ConsumerAcknowledgment>();
}

public class ConsumerAcknowledgment
{
  public int Id { get; set; }
  public string MessageId { get; set; } = string.Empty;
  public int ConsumerGroupRegistrationId { get; set; }
  public bool Success { get; set; }
  public string? ErrorMessage { get; set; }
  public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;

  // Navigation property
  public ConsumerGroupRegistration ConsumerGroupRegistration { get; set; } = null!;
}

// Request/Response DTOs
public class MessageRequest
{
  public string Topic { get; set; } = string.Empty;
  public string Message { get; set; } = string.Empty;
  public string? ConsumerGroup { get; set; } // Optional - if not provided, will send to all registered consumer groups for the topic
}

public class MessageResponse
{
  public string MessageId { get; set; } = string.Empty;
  public string Status { get; set; } = string.Empty;
  public DateTime Timestamp { get; set; } = DateTime.UtcNow;
  public string Topic { get; set; } = string.Empty;
  public List<string> TargetConsumerGroups { get; set; } = new();
}

public class AcknowledgmentRequest
{
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public bool Success { get; set; } = true;
  public string? ErrorMessage { get; set; }
}

public class TopicRegistrationRequest
{
  public string TopicName { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public List<ConsumerGroupRequest> ConsumerGroups { get; set; } = new();
}

public class ConsumerGroupRequest
{
  public string ConsumerGroupName { get; set; } = string.Empty;
  public bool RequiresAcknowledgment { get; set; } = true;
  public int AcknowledgmentTimeoutMinutes { get; set; } = 30;
  public int MaxRetries { get; set; } = 3;
}

public class TopicRegistrationResponse
{
  public int Id { get; set; }
  public string TopicName { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public bool IsActive { get; set; }
  public DateTime CreatedAt { get; set; }
  public List<ConsumerGroupResponse> ConsumerGroups { get; set; } = new();
}

public class ConsumerGroupResponse
{
  public int Id { get; set; }
  public string ConsumerGroupName { get; set; } = string.Empty;
  public bool RequiresAcknowledgment { get; set; }
  public bool IsActive { get; set; }
  public int AcknowledgmentTimeoutMinutes { get; set; }
  public int MaxRetries { get; set; }
  public DateTime CreatedAt { get; set; }
}
