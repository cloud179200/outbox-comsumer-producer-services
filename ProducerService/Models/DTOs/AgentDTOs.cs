using ProducerService.Models.Agents;

namespace ProducerService.Models.DTOs;

// Agent Management DTOs
public class AgentRegistrationRequest
{
  public string ServiceId { get; set; } = string.Empty;
  public string ServiceName { get; set; } = string.Empty;
  public string HostName { get; set; } = string.Empty;
  public string IpAddress { get; set; } = string.Empty;
  public int Port { get; set; }
  public string BaseUrl { get; set; } = string.Empty;
  public ServiceType ServiceType { get; set; }
  public string? Version { get; set; }
  public Dictionary<string, string> Metadata { get; set; } = new();
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();
}

public class AgentHeartbeatRequest
{
  public string ServiceId { get; set; } = string.Empty;
  public string InstanceId { get; set; } = string.Empty;
  public AgentStatus Status { get; set; } = AgentStatus.Active;
  public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;
  public string? StatusMessage { get; set; }
  public Dictionary<string, object> HealthData { get; set; } = new();
}

public class AgentResponse
{
  public int Id { get; set; }
  public string ServiceId { get; set; } = string.Empty;
  public string InstanceId { get; set; } = string.Empty;
  public string ServiceName { get; set; } = string.Empty;
  public string BaseUrl { get; set; } = string.Empty;
  public AgentStatus Status { get; set; }
  public DateTime StartedAt { get; set; }
  public DateTime LastHeartbeat { get; set; }
  public string? Version { get; set; }
  public ServiceType ServiceType { get; set; }
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();
}
