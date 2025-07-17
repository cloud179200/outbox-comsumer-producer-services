using Microsoft.EntityFrameworkCore;
using ProducerService.Data;
using ProducerService.Models;

namespace ProducerService.Services;

public interface ITopicRegistrationService
{
  Task<TopicRegistrationResponse?> RegisterTopicAsync(TopicRegistrationRequest request);
  Task<TopicRegistrationResponse?> UpdateTopicAsync(int topicId, TopicRegistrationRequest request);
  Task<bool> DeactivateTopicAsync(int topicId);
  Task<TopicRegistrationResponse?> GetTopicAsync(int topicId);
  Task<TopicRegistrationResponse?> GetTopicByNameAsync(string topicName);
  Task<List<TopicRegistrationResponse>> GetAllTopicsAsync(bool includeInactive = false);
  Task<bool> AddConsumerGroupToTopicAsync(int topicId, ConsumerGroupRequest request);
  Task<bool> UpdateConsumerGroupAsync(int consumerGroupId, ConsumerGroupRequest request);
  Task<bool> DeactivateConsumerGroupAsync(int consumerGroupId);
  Task<List<ConsumerGroupResponse>> GetConsumerGroupsForTopicAsync(int topicId);
  Task<List<ConsumerGroupResponse>> GetAllConsumerGroupsAsync(bool includeInactive = false);
}

public class TopicRegistrationService : ITopicRegistrationService
{
  private readonly OutboxDbContext _dbContext;
  private readonly ILogger<TopicRegistrationService> _logger;

  public TopicRegistrationService(OutboxDbContext dbContext, ILogger<TopicRegistrationService> logger)
  {
    _dbContext = dbContext;
    _logger = logger;
  }

  public async Task<TopicRegistrationResponse?> RegisterTopicAsync(TopicRegistrationRequest request)
  {
    try
    {
      // Check if topic already exists
      var existingTopic = await _dbContext.TopicRegistrations
          .FirstOrDefaultAsync(t => t.TopicName == request.TopicName);

      if (existingTopic != null)
      {
        _logger.LogWarning("Topic {TopicName} already exists", request.TopicName);
        return null;
      }

      var topicRegistration = new TopicRegistration
      {
        TopicName = request.TopicName,
        Description = request.Description,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      };

      _dbContext.TopicRegistrations.Add(topicRegistration);
      await _dbContext.SaveChangesAsync();

      // Add consumer groups
      foreach (var consumerGroupRequest in request.ConsumerGroups)
      {
        var consumerGroup = new ConsumerGroupRegistration
        {
          ConsumerGroupName = consumerGroupRequest.ConsumerGroupName,
          TopicRegistrationId = topicRegistration.Id,
          RequiresAcknowledgment = consumerGroupRequest.RequiresAcknowledgment,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = consumerGroupRequest.AcknowledgmentTimeoutMinutes,
          MaxRetries = consumerGroupRequest.MaxRetries,
          CreatedAt = DateTime.UtcNow
        };

        _dbContext.ConsumerGroupRegistrations.Add(consumerGroup);
      }

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Registered topic {TopicName} with {ConsumerGroupCount} consumer groups",
          request.TopicName, request.ConsumerGroups.Count);

      return await GetTopicAsync(topicRegistration.Id);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error registering topic {TopicName}", request.TopicName);
      return null;
    }
  }

  public async Task<TopicRegistrationResponse?> UpdateTopicAsync(int topicId, TopicRegistrationRequest request)
  {
    try
    {
      var topic = await _dbContext.TopicRegistrations
          .Include(t => t.ConsumerGroups)
          .FirstOrDefaultAsync(t => t.Id == topicId);

      if (topic == null)
      {
        _logger.LogWarning("Topic with ID {TopicId} not found", topicId);
        return null;
      }

      topic.Description = request.Description;
      topic.UpdatedAt = DateTime.UtcNow;

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Updated topic {TopicName} (ID: {TopicId})", topic.TopicName, topicId);

      return await GetTopicAsync(topicId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating topic {TopicId}", topicId);
      return null;
    }
  }

  public async Task<bool> DeactivateTopicAsync(int topicId)
  {
    try
    {
      var topic = await _dbContext.TopicRegistrations.FirstOrDefaultAsync(t => t.Id == topicId);
      if (topic == null)
        return false;

      topic.IsActive = false;
      topic.UpdatedAt = DateTime.UtcNow;

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Deactivated topic {TopicName} (ID: {TopicId})", topic.TopicName, topicId);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating topic {TopicId}", topicId);
      return false;
    }
  }

  public async Task<TopicRegistrationResponse?> GetTopicAsync(int topicId)
  {
    try
    {
      var topic = await _dbContext.TopicRegistrations
          .Include(t => t.ConsumerGroups)
          .FirstOrDefaultAsync(t => t.Id == topicId);

      if (topic == null)
        return null;

      return MapToResponse(topic);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting topic {TopicId}", topicId);
      return null;
    }
  }

  public async Task<TopicRegistrationResponse?> GetTopicByNameAsync(string topicName)
  {
    try
    {
      var topic = await _dbContext.TopicRegistrations
          .Include(t => t.ConsumerGroups)
          .FirstOrDefaultAsync(t => t.TopicName == topicName);

      if (topic == null)
        return null;

      return MapToResponse(topic);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting topic {TopicName}", topicName);
      return null;
    }
  }

  public async Task<List<TopicRegistrationResponse>> GetAllTopicsAsync(bool includeInactive = false)
  {
    try
    {
      var query = _dbContext.TopicRegistrations
          .Include(t => t.ConsumerGroups)
          .AsQueryable();

      if (!includeInactive)
      {
        query = query.Where(t => t.IsActive);
      }

      var topics = await query.OrderBy(t => t.TopicName).ToListAsync();

      return topics.Select(MapToResponse).ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting all topics");
      return new List<TopicRegistrationResponse>();
    }
  }

  public async Task<bool> AddConsumerGroupToTopicAsync(int topicId, ConsumerGroupRequest request)
  {
    try
    {
      var topic = await _dbContext.TopicRegistrations.FirstOrDefaultAsync(t => t.Id == topicId);
      if (topic == null)
        return false;

      // Check if consumer group already exists for this topic
      var existingConsumerGroup = await _dbContext.ConsumerGroupRegistrations
          .FirstOrDefaultAsync(cg => cg.TopicRegistrationId == topicId &&
                                    cg.ConsumerGroupName == request.ConsumerGroupName);

      if (existingConsumerGroup != null)
      {
        _logger.LogWarning("Consumer group {ConsumerGroupName} already exists for topic {TopicId}",
            request.ConsumerGroupName, topicId);
        return false;
      }

      var consumerGroup = new ConsumerGroupRegistration
      {
        ConsumerGroupName = request.ConsumerGroupName,
        TopicRegistrationId = topicId,
        RequiresAcknowledgment = request.RequiresAcknowledgment,
        IsActive = true,
        AcknowledgmentTimeoutMinutes = request.AcknowledgmentTimeoutMinutes,
        MaxRetries = request.MaxRetries,
        CreatedAt = DateTime.UtcNow
      };

      _dbContext.ConsumerGroupRegistrations.Add(consumerGroup);
      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Added consumer group {ConsumerGroupName} to topic {TopicName}",
          request.ConsumerGroupName, topic.TopicName);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error adding consumer group {ConsumerGroupName} to topic {TopicId}",
          request.ConsumerGroupName, topicId);
      return false;
    }
  }

  public async Task<bool> UpdateConsumerGroupAsync(int consumerGroupId, ConsumerGroupRequest request)
  {
    try
    {
      var consumerGroup = await _dbContext.ConsumerGroupRegistrations
          .FirstOrDefaultAsync(cg => cg.Id == consumerGroupId);

      if (consumerGroup == null)
        return false;

      consumerGroup.RequiresAcknowledgment = request.RequiresAcknowledgment;
      consumerGroup.AcknowledgmentTimeoutMinutes = request.AcknowledgmentTimeoutMinutes;
      consumerGroup.MaxRetries = request.MaxRetries;
      consumerGroup.UpdatedAt = DateTime.UtcNow;

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Updated consumer group {ConsumerGroupName} (ID: {ConsumerGroupId})",
          consumerGroup.ConsumerGroupName, consumerGroupId);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating consumer group {ConsumerGroupId}", consumerGroupId);
      return false;
    }
  }

  public async Task<bool> DeactivateConsumerGroupAsync(int consumerGroupId)
  {
    try
    {
      var consumerGroup = await _dbContext.ConsumerGroupRegistrations
          .FirstOrDefaultAsync(cg => cg.Id == consumerGroupId);

      if (consumerGroup == null)
        return false;

      consumerGroup.IsActive = false;
      consumerGroup.UpdatedAt = DateTime.UtcNow;

      await _dbContext.SaveChangesAsync();

      _logger.LogInformation("Deactivated consumer group {ConsumerGroupName} (ID: {ConsumerGroupId})",
          consumerGroup.ConsumerGroupName, consumerGroupId);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating consumer group {ConsumerGroupId}", consumerGroupId);
      return false;
    }
  }
  public async Task<List<ConsumerGroupResponse>> GetConsumerGroupsForTopicAsync(int topicId)
  {
    try
    {
      var consumerGroups = await _dbContext.ConsumerGroupRegistrations
          .Where(cg => cg.TopicRegistrationId == topicId)
          .OrderBy(cg => cg.ConsumerGroupName)
          .ToListAsync();

      return consumerGroups.Select(cg => new ConsumerGroupResponse
      {
        Id = cg.Id,
        ConsumerGroupName = cg.ConsumerGroupName,
        RequiresAcknowledgment = cg.RequiresAcknowledgment,
        IsActive = cg.IsActive,
        AcknowledgmentTimeoutMinutes = cg.AcknowledgmentTimeoutMinutes,
        MaxRetries = cg.MaxRetries,
        CreatedAt = cg.CreatedAt
      }).ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting consumer groups for topic {TopicId}", topicId);
      return new List<ConsumerGroupResponse>();
    }
  }

  public async Task<List<ConsumerGroupResponse>> GetAllConsumerGroupsAsync(bool includeInactive = false)
  {
    try
    {
      var query = _dbContext.ConsumerGroupRegistrations
          .Include(cg => cg.TopicRegistration)
          .AsQueryable();

      if (!includeInactive)
      {
        query = query.Where(cg => cg.IsActive);
      }

      var consumerGroups = await query
          .OrderBy(cg => cg.ConsumerGroupName)
          .ToListAsync();

      return consumerGroups.Select(cg => new ConsumerGroupResponse
      {
        Id = cg.Id,
        ConsumerGroupName = cg.ConsumerGroupName,
        RequiresAcknowledgment = cg.RequiresAcknowledgment,
        IsActive = cg.IsActive,
        AcknowledgmentTimeoutMinutes = cg.AcknowledgmentTimeoutMinutes,
        MaxRetries = cg.MaxRetries,
        CreatedAt = cg.CreatedAt
      }).ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting all consumer groups");
      return new List<ConsumerGroupResponse>();
    }
  }

  private TopicRegistrationResponse MapToResponse(TopicRegistration topic)
  {
    return new TopicRegistrationResponse
    {
      Id = topic.Id,
      TopicName = topic.TopicName,
      Description = topic.Description,
      IsActive = topic.IsActive,
      CreatedAt = topic.CreatedAt,
      ConsumerGroups = topic.ConsumerGroups.Select(cg => new ConsumerGroupResponse
      {
        Id = cg.Id,
        ConsumerGroupName = cg.ConsumerGroupName,
        RequiresAcknowledgment = cg.RequiresAcknowledgment,
        IsActive = cg.IsActive,
        AcknowledgmentTimeoutMinutes = cg.AcknowledgmentTimeoutMinutes,
        MaxRetries = cg.MaxRetries,
        CreatedAt = cg.CreatedAt
      }).ToList()
    };
  }
}
