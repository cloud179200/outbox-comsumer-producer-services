namespace ProducerService.Models.DTOs;

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
