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
  Task<List<OutboxMessage>> CreateMessagesBulkAsync(List<MessageRequest> requests);
  Task<int> CleanupOldMessagesAsync(int retentionDays);
  Task<OutboxMessage?> CreateRetryMessageAsync(OutboxMessage originalMessage, string? targetConsumerServiceId = null);
}

public class OutboxPostgreSqlService : IOutboxService
{
  private readonly OutboxDbContext _dbContext;
  private readonly ILogger<OutboxPostgreSqlService> _logger;
  private readonly string _currentServiceId;
  private readonly string _currentInstanceId;

  /// <summary>
  /// Initializes a new instance of the OutboxPostgreSqlService with database context and logger.
  /// Sets up service identification from environment variables for tracking message origins.
  /// </summary>
  /// <param name="dbContext">The database context for accessing outbox tables</param>
  /// <param name="logger">Logger instance for tracking service operations</param>
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

  /// <summary>
  /// Creates outbox messages for a specific topic and distributes them to appropriate consumer groups.
  /// Handles both targeted delivery to specific consumer groups and broadcast to all active groups.
  /// </summary>
  /// <param name="topicName">The topic name to create messages for</param>
  /// <param name="message">The message content to be delivered</param>
  /// <param name="specificConsumerGroup">Optional: Target a specific consumer group only</param>
  /// <returns>List of created outbox messages, empty if topic not found or no active consumer groups</returns>
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

  /// <summary>
  /// Performs bulk creation of outbox messages from multiple message requests efficiently.
  /// Optimizes database operations by batching inserts and validating topic registrations upfront.
  /// </summary>
  /// <param name="requests">List of message requests to process</param>
  /// <returns>List of all created outbox messages across all requests</returns>
  public async Task<List<OutboxMessage>> CreateMessagesBulkAsync(List<MessageRequest> requests)
  {
    try
    {
      var allMessages = new List<OutboxMessage>();
      var requestsToProcess = new List<(MessageRequest request, TopicRegistration topic, List<ConsumerGroupRegistration> groups)>();

      // First, validate and collect all topic registrations and consumer groups
      foreach (var request in requests)
      {
        var topicRegistration = await _dbContext.TopicRegistrations
          .Include(t => t.ConsumerGroups.Where(cg => cg.IsActive))
          .FirstOrDefaultAsync(t => t.TopicName == request.Topic && t.IsActive);

        if (topicRegistration == null)
        {
          _logger.LogWarning("Topic registration not found for topic: {TopicName}", request.Topic);
          continue;
        }

        var targetConsumerGroups = request.ConsumerGroup != null
          ? topicRegistration.ConsumerGroups.Where(cg => cg.ConsumerGroupName == request.ConsumerGroup).ToList()
          : topicRegistration.ConsumerGroups.ToList();

        if (!targetConsumerGroups.Any())
        {
          _logger.LogWarning("No active consumer groups found for topic: {TopicName}, specificGroup: {SpecificGroup}",
            request.Topic, request.ConsumerGroup);
          continue;
        }

        requestsToProcess.Add((request, topicRegistration, targetConsumerGroups));
      }

      // Create all outbox messages in memory first
      foreach (var (request, topicRegistration, consumerGroups) in requestsToProcess)
      {
        foreach (var consumerGroup in consumerGroups)
        {
          var outboxMessage = new OutboxMessage
          {
            Id = Guid.NewGuid().ToString(),
            Topic = request.Topic,
            Message = request.Message,
            ConsumerGroup = consumerGroup.ConsumerGroupName,
            TopicRegistrationId = topicRegistration.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProducerServiceId = _currentServiceId,
            ProducerInstanceId = _currentInstanceId
          };

          allMessages.Add(outboxMessage);
        }
      }

      // Bulk insert all messages in a single transaction
      if (allMessages.Any())
      {
        _dbContext.OutboxMessages.AddRange(allMessages);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Bulk created {Count} outbox messages from {RequestCount} requests",
          allMessages.Count, requests.Count);
      }

      return allMessages;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error bulk creating messages for {RequestCount} requests", requests.Count);
      return new List<OutboxMessage>();
    }
  }

  /// <summary>
  /// Adds a single outbox message to the database.
  /// Used for direct message insertion when bypassing topic-based distribution.
  /// </summary>
  /// <param name="message">The outbox message to add</param>
  /// <returns>True if successfully added, false on error</returns>
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

  /// <summary>
  /// Retrieves a specific outbox message by its unique identifier.
  /// Includes related topic registration information for complete context.
  /// </summary>
  /// <param name="messageId">The unique identifier of the message to retrieve</param>
  /// <returns>The outbox message if found, null otherwise</returns>
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

  /// <summary>
  /// Updates the status of an outbox message with optional error information.
  /// Handles status transitions and retry count management for failed messages.
  /// </summary>
  /// <param name="messageId">The unique identifier of the message to update</param>
  /// <param name="status">The new status to set for the message</param>
  /// <param name="errorMessage">Optional error message for failed status updates</param>
  /// <returns>True if successfully updated, false if message not found or error occurred</returns>
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
  /// <summary>
  /// Retrieves pending outbox messages that are ready for processing by the Kafka producer.
  /// Filters by current service instance and orders by creation time for fair processing.
  /// </summary>
  /// <param name="limit">Maximum number of messages to retrieve (default: 100)</param>
  /// <returns>List of pending outbox messages ready for processing</returns>
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

  /// <summary>
  /// Finds messages that have been sent but not acknowledged within the specified timeout period.
  /// Used by retry mechanisms to identify messages that may need reprocessing.
  /// </summary>
  /// <param name="consumerGroup">The consumer group to check for unacknowledged messages</param>
  /// <param name="timeout">Time period after which messages are considered unacknowledged</param>
  /// <returns>List of messages that may need retry processing</returns>
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

  /// <summary>
  /// Permanently removes an outbox message from the database.
  /// Use with caution as this action cannot be undone.
  /// </summary>
  /// <param name="messageId">The unique identifier of the message to delete</param>
  /// <returns>True if successfully deleted, false if message not found or error occurred</returns>
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
  /// <summary>
  /// Retrieves messages targeted for a specific consumer group for monitoring and debugging.
  /// Orders by most recent first to show latest activity.
  /// </summary>
  /// <param name="consumerGroup">The consumer group to retrieve messages for</param>
  /// <param name="limit">Maximum number of messages to retrieve (default: 100)</param>
  /// <returns>List of messages for the specified consumer group</returns>
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
  /// <summary>
  /// Removes old outbox messages to prevent database bloat and maintain performance.
  /// Only removes messages that are in terminal states (acknowledged, failed, or expired).
  /// </summary>
  /// <param name="retentionDays">Number of days to retain messages before cleanup</param>
  /// <returns>Number of messages that were cleaned up</returns>
  public async Task<int> CleanupOldMessagesAsync(int retentionDays)
  {
    try
    {
      var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

      var oldMessages = await _dbContext.OutboxMessages
        .Where(m => (m.Status == OutboxMessageStatus.Acknowledged || m.Status == OutboxMessageStatus.Failed || m.Status == OutboxMessageStatus.Expired) &&
                   m.CreatedAt < cutoffDate)
        .ToListAsync();

      if (oldMessages.Any())
      {
        _dbContext.OutboxMessages.RemoveRange(oldMessages);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old messages older than {Days} days", oldMessages.Count, retentionDays);
      }

      return oldMessages.Count;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error cleaning up old messages");
      return 0;
    }
  }

  public async Task<OutboxMessage?> CreateRetryMessageAsync(OutboxMessage originalMessage, string? targetConsumerServiceId = null)
  {
    try
    {
      // Create a retry message with targeting
      var retryMessage = new OutboxMessage
      {
        Id = Guid.NewGuid().ToString(),
        Topic = originalMessage.Topic,
        Message = originalMessage.Message,
        ConsumerGroup = originalMessage.ConsumerGroup,
        TopicRegistrationId = originalMessage.TopicRegistrationId,
        Status = OutboxMessageStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        ProducerServiceId = _currentServiceId,
        ProducerInstanceId = _currentInstanceId,
        IsRetry = true,
        TargetConsumerServiceId = targetConsumerServiceId,
        OriginalMessageId = originalMessage.Id,
        RetryCount = originalMessage.RetryCount + 1,
        IdempotencyKey = $"retry-{originalMessage.Id}-{originalMessage.RetryCount + 1}"
      };

      _dbContext.OutboxMessages.Add(retryMessage);

      // Mark original message as being retried
      originalMessage.Status = OutboxMessageStatus.Failed;
      originalMessage.ErrorMessage = $"Retrying with message {retryMessage.Id}";
      originalMessage.RetryCount++;
      originalMessage.LastRetryAt = DateTime.UtcNow;

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Created retry message {RetryMessageId} for original message {OriginalMessageId} targeting consumer {TargetConsumer}",
        retryMessage.Id, originalMessage.Id, targetConsumerServiceId ?? "any");

      return retryMessage;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating retry message for {OriginalMessageId}", originalMessage.Id);
      return null;
    }
  }
}
