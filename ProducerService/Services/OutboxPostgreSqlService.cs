using Microsoft.EntityFrameworkCore;
using ProducerService.Data;
using ProducerService.Models;

namespace ProducerService.Services;

public interface IOutboxService
{
  Task<bool> AddMessageAsync(OutboxMessage message);
  Task<OutboxMessage?> GetMessageAsync(string messageId);
  Task<bool> UpdateMessageStatusAsync(string messageId, OutboxMessageStatus status, string? errorMessage = null);
  Task<List<OutboxMessage>> GetPendingMessagesAsync(int limit = 100);
  Task<List<OutboxMessage>> GetUnacknowledgedMessagesAsync(string consumerGroup, TimeSpan timeout);
  Task<bool> DeleteMessageAsync(string messageId);
  Task<List<OutboxMessage>> GetMessagesForConsumerGroupAsync(string consumerGroup, int limit = 100);
  Task<List<OutboxMessage>> CreateMessagesForTopicAsync(string topicName, string message, string? specificConsumerGroup = null);
}

public class OutboxPostgreSqlService : IOutboxService
{
  private readonly OutboxDbContext _dbContext;
  private readonly ILogger<OutboxPostgreSqlService> _logger;
  private readonly string _currentServiceId;
  private readonly string _currentInstanceId;

  public OutboxPostgreSqlService(OutboxDbContext dbContext, ILogger<OutboxPostgreSqlService> logger)
  {
    _dbContext = dbContext;
    _logger = logger;

    // Get current service identification from environment
    _currentServiceId = Environment.GetEnvironmentVariable("SERVICE_ID")
        ?? Environment.GetEnvironmentVariable("PRODUCER_SERVICE_ID")
        ?? $"producer-{Environment.MachineName}";
    _currentInstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
        ?? $"{_currentServiceId}-{Guid.NewGuid():N}";
  }

  public async Task<List<OutboxMessage>> CreateMessagesForTopicAsync(string topicName, string message, string? specificConsumerGroup = null)
  {
    try
    {
      var topicRegistration = await _dbContext.TopicRegistrations
        .Include(t => t.ConsumerGroups.Where(cg => cg.IsActive))
        .FirstOrDefaultAsync(t => t.TopicName == topicName && t.IsActive);

      if (topicRegistration == null)
      {
        _logger.LogWarning("Topic registration not found for topic: {TopicName}", topicName);
        return new List<OutboxMessage>();
      }

      var targetConsumerGroups = specificConsumerGroup != null
        ? topicRegistration.ConsumerGroups.Where(cg => cg.ConsumerGroupName == specificConsumerGroup).ToList()
        : topicRegistration.ConsumerGroups.ToList();

      if (!targetConsumerGroups.Any())
      {
        _logger.LogWarning("No active consumer groups found for topic: {TopicName}, specificGroup: {SpecificGroup}",
          topicName, specificConsumerGroup);
        return new List<OutboxMessage>();
      }

      var messages = new List<OutboxMessage>();

      foreach (var consumerGroup in targetConsumerGroups)
      {
        var outboxMessage = new OutboxMessage
        {
          Id = Guid.NewGuid().ToString(),
          Topic = topicName,
          Message = message,
          ConsumerGroup = consumerGroup.ConsumerGroupName,
          TopicRegistrationId = topicRegistration.Id,
          Status = OutboxMessageStatus.Pending,
          CreatedAt = DateTime.UtcNow,
          ProducerServiceId = _currentServiceId,
          ProducerInstanceId = _currentInstanceId
        };

        _dbContext.OutboxMessages.Add(outboxMessage);
        messages.Add(outboxMessage);
      }

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Created {Count} outbox messages for topic {TopicName}",
        messages.Count, topicName);

      return messages;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating messages for topic {TopicName}", topicName);
      return new List<OutboxMessage>();
    }
  }

  public async Task<bool> AddMessageAsync(OutboxMessage message)
  {
    try
    {
      _dbContext.OutboxMessages.Add(message);
      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Added message {MessageId} to outbox for consumer group {ConsumerGroup}",
        message.Id, message.ConsumerGroup);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error adding message {MessageId} to outbox", message.Id);
      return false;
    }
  }

  public async Task<OutboxMessage?> GetMessageAsync(string messageId)
  {
    try
    {
      return await _dbContext.OutboxMessages
        .Include(m => m.TopicRegistration)
        .FirstOrDefaultAsync(m => m.Id == messageId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting message {MessageId} from outbox", messageId);
      return null;
    }
  }

  public async Task<bool> UpdateMessageStatusAsync(string messageId, OutboxMessageStatus status, string? errorMessage = null)
  {
    try
    {
      var message = await _dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);
      if (message == null)
        return false;

      message.Status = status;
      message.ProcessedAt = DateTime.UtcNow;
      message.ErrorMessage = errorMessage;

      if (status == OutboxMessageStatus.Failed)
      {
        message.RetryCount++;
        message.LastRetryAt = DateTime.UtcNow;
      }

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Updated message {MessageId} status to {Status}", messageId, status);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating message {MessageId} status", messageId);
      return false;
    }
  }
  public async Task<List<OutboxMessage>> GetPendingMessagesAsync(int limit = 100)
  {
    try
    {
      return await _dbContext.OutboxMessages
        .Include(m => m.TopicRegistration)
        .Where(m => m.Status == OutboxMessageStatus.Pending &&
                    m.ProducerServiceId == _currentServiceId)
        .OrderBy(m => m.CreatedAt)
        .Take(limit)
        .ToListAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting pending messages from outbox");
      return new List<OutboxMessage>();
    }
  }

  public async Task<List<OutboxMessage>> GetUnacknowledgedMessagesAsync(string consumerGroup, TimeSpan timeout)
  {
    try
    {
      var cutoffTime = DateTime.UtcNow.Subtract(timeout);

      return await _dbContext.OutboxMessages
        .Include(m => m.TopicRegistration)
        .Where(m => m.ConsumerGroup == consumerGroup &&
                   m.Status == OutboxMessageStatus.Sent &&
                   m.ProcessedAt < cutoffTime)
        .OrderBy(m => m.ProcessedAt)
        .ToListAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting unacknowledged messages for consumer group {ConsumerGroup}", consumerGroup);
      return new List<OutboxMessage>();
    }
  }

  public async Task<bool> DeleteMessageAsync(string messageId)
  {
    try
    {
      var message = await _dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);
      if (message == null)
        return false;

      _dbContext.OutboxMessages.Remove(message);
      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Deleted message {MessageId} from outbox", messageId);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting message {MessageId} from outbox", messageId);
      return false;
    }
  }

  public async Task<List<OutboxMessage>> GetMessagesForConsumerGroupAsync(string consumerGroup, int limit = 100)
  {
    try
    {
      return await _dbContext.OutboxMessages
        .Include(m => m.TopicRegistration)
        .Where(m => m.ConsumerGroup == consumerGroup)
        .OrderByDescending(m => m.CreatedAt)
        .Take(limit)
        .ToListAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting messages for consumer group {ConsumerGroup}", consumerGroup);
      return new List<OutboxMessage>();
    }
  }
}
