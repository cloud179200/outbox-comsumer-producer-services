namespace ConsumerService.Models.DTOs;

/// <summary>
/// Configuration object that defines a consumer group and its associated topics.
/// Used to configure which topics a consumer group should process messages from.
/// </summary>
public class ConsumerGroupConfig
{
  /// <summary>
  /// Unique name of the consumer group
  /// </summary>
  public string GroupName { get; set; } = string.Empty;

  /// <summary>
  /// Array of topic names this consumer group should process
  /// </summary>
  public string[] Topics { get; set; } = Array.Empty<string>();
}
