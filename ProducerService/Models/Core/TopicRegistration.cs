namespace ProducerService.Models.Core;

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
