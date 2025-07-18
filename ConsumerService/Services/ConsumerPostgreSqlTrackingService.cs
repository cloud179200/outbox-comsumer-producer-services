using Microsoft.EntityFrameworkCore;
using ConsumerService.Data;
using ConsumerService.Models;

namespace ConsumerService.Services;

public interface IConsumerTrackingService
{
  Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup);
  Task<bool> IsMessageProcessedByIdempotencyKeyAsync(string idempotencyKey, string consumerGroup);
  Task MarkMessageAsProcessingAsync(ConsumerMessage message);
  Task MarkMessageAsProcessedAsync(string messageId, string consumerGroup, string topic, string? content = null, string? producerServiceId = null, string? producerInstanceId = null, string idempotencyKey = "");
  Task MarkMessageAsFailedAsync(string messageId, string consumerGroup, string topic, string errorMessage, string? content = null, string? producerServiceId = null, string? producerInstanceId = null);
  Task<List<ProcessedMessage>> GetProcessedMessagesAsync(string consumerGroup, int limit = 100);
  Task<List<FailedMessage>> GetFailedMessagesAsync(string consumerGroup, int limit = 100);
}

public class ConsumerPostgreSqlTrackingService : IConsumerTrackingService
{
  private readonly ConsumerDbContext _dbContext;
  private readonly ILogger<ConsumerPostgreSqlTrackingService> _logger;
  private readonly IConfiguration _configuration;

  public ConsumerPostgreSqlTrackingService(ConsumerDbContext dbContext, ILogger<ConsumerPostgreSqlTrackingService> logger, IConfiguration configuration)
  {
    _dbContext = dbContext;
    _logger = logger;
    _configuration = configuration;
  }
  public async Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup)
  {
    try
    {
      var isProcessed = await _dbContext.ProcessedMessages
          .AnyAsync(p => p.MessageId == messageId && p.ConsumerGroup == consumerGroup);

      _logger.LogDebug("Message {MessageId} processed status for consumer group {ConsumerGroup}: {IsProcessed}",
          messageId, consumerGroup, isProcessed);

      return isProcessed;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking if message {MessageId} is processed for consumer group {ConsumerGroup}",
          messageId, consumerGroup);
      return false;
    }
  }

  public async Task<bool> IsMessageProcessedByIdempotencyKeyAsync(string idempotencyKey, string consumerGroup)
  {
    try
    {
      if (string.IsNullOrEmpty(idempotencyKey))
        return false;

      var isProcessed = await _dbContext.ProcessedMessages
          .AnyAsync(p => p.IdempotencyKey == idempotencyKey && p.ConsumerGroup == consumerGroup);

      _logger.LogDebug("Message with idempotency key {IdempotencyKey} processed status for consumer group {ConsumerGroup}: {IsProcessed}",
          idempotencyKey, consumerGroup, isProcessed);

      return isProcessed;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking if message with idempotency key {IdempotencyKey} is processed for consumer group {ConsumerGroup}",
          idempotencyKey, consumerGroup); return false;
    }
  }

  public async Task MarkMessageAsProcessingAsync(ConsumerMessage message)
  {
    try
    {
      // For PostgreSQL, we don't need to track "processing" state separately
      // We can rely on transaction handling and the processed state
      _logger.LogDebug("Marking message {MessageId} as processing for consumer group {ConsumerGroup}",
          message.MessageId, message.ConsumerGroup);

      await Task.CompletedTask;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error marking message {MessageId} as processing for consumer group {ConsumerGroup}",
          message.MessageId, message.ConsumerGroup);
    }
  }
  public async Task MarkMessageAsProcessedAsync(string messageId, string consumerGroup, string topic, string? content = null, string? producerServiceId = null, string? producerInstanceId = null, string idempotencyKey = "")
  {
    try
    {
      // Get consumer service information
      var consumerServiceId = Environment.GetEnvironmentVariable("SERVICE_ID")
          ?? Environment.GetEnvironmentVariable("CONSUMER_SERVICE_ID")
          ?? $"consumer-{Environment.MachineName}";

      var consumerInstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
          ?? $"{consumerServiceId}-{Guid.NewGuid():N}";

      var processedMessage = new ProcessedMessage
      {
        MessageId = messageId,
        ConsumerGroup = consumerGroup,
        Topic = topic,
        ProcessedAt = DateTime.UtcNow,
        Content = content,
        ProducerServiceId = producerServiceId ?? "",
        ProducerInstanceId = producerInstanceId ?? "",
        ConsumerServiceId = consumerServiceId,
        ConsumerInstanceId = consumerInstanceId,
        IdempotencyKey = idempotencyKey
      };

      // Use upsert pattern to handle duplicates
      var existing = await _dbContext.ProcessedMessages
          .FirstOrDefaultAsync(p => p.MessageId == messageId && p.ConsumerGroup == consumerGroup);

      if (existing == null)
      {
        _dbContext.ProcessedMessages.Add(processedMessage);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Marked message {MessageId} as processed for consumer group {ConsumerGroup} by {ConsumerServiceId}",
            messageId, consumerGroup, consumerServiceId);
      }
      else
      {
        _logger.LogDebug("Message {MessageId} already marked as processed for consumer group {ConsumerGroup}",
            messageId, consumerGroup);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error marking message {MessageId} as processed for consumer group {ConsumerGroup}",
          messageId, consumerGroup);
    }
  }
  public async Task MarkMessageAsFailedAsync(string messageId, string consumerGroup, string topic, string errorMessage, string? content = null, string? producerServiceId = null, string? producerInstanceId = null)
  {
    try
    {
      // Get consumer service information
      var consumerServiceId = Environment.GetEnvironmentVariable("SERVICE_ID")
          ?? Environment.GetEnvironmentVariable("CONSUMER_SERVICE_ID")
          ?? $"consumer-{Environment.MachineName}";

      var consumerInstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
          ?? $"{consumerServiceId}-{Guid.NewGuid():N}";

      // Check if we already have a failed record for this message
      var existingFailed = await _dbContext.FailedMessages
          .FirstOrDefaultAsync(f => f.MessageId == messageId && f.ConsumerGroup == consumerGroup);

      if (existingFailed != null)
      {
        // Update retry count and error message
        existingFailed.RetryCount++;
        existingFailed.ErrorMessage = errorMessage;
        existingFailed.FailedAt = DateTime.UtcNow;
        existingFailed.ProducerServiceId = producerServiceId ?? existingFailed.ProducerServiceId;
        existingFailed.ProducerInstanceId = producerInstanceId ?? existingFailed.ProducerInstanceId;
        existingFailed.ConsumerServiceId = consumerServiceId;
        existingFailed.ConsumerInstanceId = consumerInstanceId;
      }
      else
      {
        var failedMessage = new FailedMessage
        {
          MessageId = messageId,
          ConsumerGroup = consumerGroup,
          Topic = topic,
          ErrorMessage = errorMessage,
          FailedAt = DateTime.UtcNow,
          RetryCount = 1,
          Content = content,
          ProducerServiceId = producerServiceId ?? "",
          ProducerInstanceId = producerInstanceId ?? "",
          ConsumerServiceId = consumerServiceId,
          ConsumerInstanceId = consumerInstanceId
        };

        _dbContext.FailedMessages.Add(failedMessage);
      }

      await _dbContext.SaveChangesAsync();

      _logger.LogWarning("Marked message {MessageId} as failed for consumer group {ConsumerGroup} by {ConsumerServiceId}: {ErrorMessage}",
          messageId, consumerGroup, consumerServiceId, errorMessage);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error marking message {MessageId} as failed for consumer group {ConsumerGroup}",
          messageId, consumerGroup);
    }
  }

  public async Task<List<ProcessedMessage>> GetProcessedMessagesAsync(string consumerGroup, int limit = 100)
  {
    try
    {
      var processedMessages = await _dbContext.ProcessedMessages
          .Where(p => p.ConsumerGroup == consumerGroup)
          .OrderByDescending(p => p.ProcessedAt)
          .Take(limit)
          .ToListAsync();

      return processedMessages;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting processed messages for consumer group {ConsumerGroup}", consumerGroup);
      return new List<ProcessedMessage>();
    }
  }

  public async Task<List<FailedMessage>> GetFailedMessagesAsync(string consumerGroup, int limit = 100)
  {
    try
    {
      var failedMessages = await _dbContext.FailedMessages
          .Where(f => f.ConsumerGroup == consumerGroup)
          .OrderByDescending(f => f.FailedAt)
          .Take(limit)
          .ToListAsync();

      return failedMessages;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting failed messages for consumer group {ConsumerGroup}", consumerGroup);
      return new List<FailedMessage>();
    }
  }
}
