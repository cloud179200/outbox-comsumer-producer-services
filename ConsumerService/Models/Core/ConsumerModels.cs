namespace ConsumerService.Models.Core;

public class AcknowledgmentRequest
{
  public string MessageId { get; set; } = string.Empty;
  public string ConsumerGroup { get; set; } = string.Empty;
  public bool Success { get; set; } = true;
  public string? ErrorMessage { get; set; }
}

public class ConsumerGroupConfig
{
  public string GroupName { get; set; } = string.Empty;
  public string[] Topics { get; set; } = Array.Empty<string>();
}

// Agent Management Models for Horizontal Scaling
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
