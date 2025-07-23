namespace ProducerService.Models.Agents;

public class ProducerServiceAgent
{
  public int Id { get; set; }
  public string ServiceId { get; set; } = string.Empty;
  public string InstanceId { get; set; } = string.Empty;
  public string ServiceName { get; set; } = string.Empty;
  public string HostName { get; set; } = string.Empty;
  public string IpAddress { get; set; } = string.Empty;
  public int Port { get; set; }
  public string BaseUrl { get; set; } = string.Empty;
  public AgentStatus Status { get; set; } = AgentStatus.Active;
  public DateTime StartedAt { get; set; } = DateTime.UtcNow;
  public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
  public string? Version { get; set; }
  public Dictionary<string, string> Metadata { get; set; } = new();

  // Navigation properties
  public ICollection<Core.OutboxMessage> Messages { get; set; } = new List<Core.OutboxMessage>();
  public ICollection<ServiceHealthCheck> HealthChecks { get; set; } = new List<ServiceHealthCheck>();
}

public class ConsumerServiceAgent
{
  public int Id { get; set; }
  public string ServiceId { get; set; } = string.Empty;
  public string InstanceId { get; set; } = string.Empty;
  public string ServiceName { get; set; } = string.Empty;
  public string HostName { get; set; } = string.Empty;
  public string IpAddress { get; set; } = string.Empty;
  public int Port { get; set; }
  public string BaseUrl { get; set; } = string.Empty;
  public AgentStatus Status { get; set; } = AgentStatus.Active;
  public DateTime StartedAt { get; set; } = DateTime.UtcNow;
  public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
  public string[] AssignedConsumerGroups { get; set; } = Array.Empty<string>();
  public string[] AssignedTopics { get; set; } = Array.Empty<string>();
  public string? Version { get; set; }
  public Dictionary<string, string> Metadata { get; set; } = new();

  // Navigation properties
  public ICollection<ServiceHealthCheck> HealthChecks { get; set; } = new List<ServiceHealthCheck>();
}

public class ServiceHealthCheck
{
  public int Id { get; set; }
  public string ServiceId { get; set; } = string.Empty;
  public string InstanceId { get; set; } = string.Empty;
  public ServiceType ServiceType { get; set; }
  public HealthStatus Status { get; set; }
  public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
  public string? StatusMessage { get; set; }
  public double ResponseTimeMs { get; set; }
  public Dictionary<string, object> HealthData { get; set; } = new();
}

public enum AgentStatus
{
  Active,
  Inactive,
  Unhealthy,
  Maintenance,
  Terminated
}

public enum ServiceType
{
  Producer,
  Consumer
}

public enum HealthStatus
{
  Healthy,
  Degraded,
  Unhealthy,
  Unknown
}
