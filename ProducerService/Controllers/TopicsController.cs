using Microsoft.AspNetCore.Mvc;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TopicsController : ControllerBase
{
  private readonly ITopicRegistrationService _topicRegistrationService;
  private readonly ILogger<TopicsController> _logger;

  public TopicsController(ITopicRegistrationService topicRegistrationService, ILogger<TopicsController> logger)
  {
    _topicRegistrationService = topicRegistrationService;
    _logger = logger;
  }

  [HttpPost("register")]
  public async Task<ActionResult<TopicRegistrationResponse>> RegisterTopic([FromBody] TopicRegistrationRequest request)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(request.TopicName))
      {
        return BadRequest("Topic name is required");
      }

      var result = await _topicRegistrationService.RegisterTopicAsync(request);

      if (result == null)
      {
        return Conflict($"Topic '{request.TopicName}' already exists");
      }

      _logger.LogInformation("Topic {TopicName} registered successfully with {ConsumerGroupCount} consumer groups",
          request.TopicName, request.ConsumerGroups.Count);

      return CreatedAtAction(nameof(GetTopic), new { id = result.Id }, result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error registering topic {TopicName}", request.TopicName);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet]
  public async Task<ActionResult<List<TopicRegistrationResponse>>> GetAllTopics([FromQuery] bool includeInactive = false)
  {
    try
    {
      var topics = await _topicRegistrationService.GetAllTopicsAsync(includeInactive);
      return Ok(topics);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting all topics");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("{id}")]
  public async Task<ActionResult<TopicRegistrationResponse>> GetTopic(int id)
  {
    try
    {
      var topic = await _topicRegistrationService.GetTopicAsync(id);

      if (topic == null)
      {
        return NotFound($"Topic with ID {id} not found");
      }

      return Ok(topic);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting topic {TopicId}", id);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("by-name/{topicName}")]
  public async Task<ActionResult<TopicRegistrationResponse>> GetTopicByName(string topicName)
  {
    try
    {
      var topic = await _topicRegistrationService.GetTopicByNameAsync(topicName);

      if (topic == null)
      {
        return NotFound($"Topic '{topicName}' not found");
      }

      return Ok(topic);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting topic {TopicName}", topicName);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPut("{id}")]
  public async Task<ActionResult<TopicRegistrationResponse>> UpdateTopic(int id, [FromBody] TopicRegistrationRequest request)
  {
    try
    {
      var result = await _topicRegistrationService.UpdateTopicAsync(id, request);

      if (result == null)
      {
        return NotFound($"Topic with ID {id} not found");
      }

      _logger.LogInformation("Topic {TopicId} updated successfully", id);
      return Ok(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating topic {TopicId}", id);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpDelete("{id}")]
  public async Task<ActionResult> DeactivateTopic(int id)
  {
    try
    {
      var success = await _topicRegistrationService.DeactivateTopicAsync(id);

      if (!success)
      {
        return NotFound($"Topic with ID {id} not found");
      }

      _logger.LogInformation("Topic {TopicId} deactivated successfully", id);
      return Ok(new { Message = "Topic deactivated successfully" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating topic {TopicId}", id);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPost("{topicId}/consumer-groups")]
  public async Task<ActionResult> AddConsumerGroup(int topicId, [FromBody] ConsumerGroupRequest request)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(request.ConsumerGroupName))
      {
        return BadRequest("Consumer group name is required");
      }

      var success = await _topicRegistrationService.AddConsumerGroupToTopicAsync(topicId, request);

      if (!success)
      {
        return NotFound($"Topic with ID {topicId} not found or consumer group already exists");
      }

      _logger.LogInformation("Consumer group {ConsumerGroupName} added to topic {TopicId}",
          request.ConsumerGroupName, topicId);

      return Ok(new { Message = "Consumer group added successfully" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error adding consumer group {ConsumerGroupName} to topic {TopicId}",
          request.ConsumerGroupName, topicId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("{topicId}/consumer-groups")]
  public async Task<ActionResult<List<ConsumerGroupResponse>>> GetConsumerGroups(int topicId)
  {
    try
    {
      var consumerGroups = await _topicRegistrationService.GetConsumerGroupsForTopicAsync(topicId);
      return Ok(consumerGroups);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting consumer groups for topic {TopicId}", topicId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPut("consumer-groups/{consumerGroupId}")]
  public async Task<ActionResult> UpdateConsumerGroup(int consumerGroupId, [FromBody] ConsumerGroupRequest request)
  {
    try
    {
      var success = await _topicRegistrationService.UpdateConsumerGroupAsync(consumerGroupId, request);

      if (!success)
      {
        return NotFound($"Consumer group with ID {consumerGroupId} not found");
      }

      _logger.LogInformation("Consumer group {ConsumerGroupId} updated successfully", consumerGroupId);
      return Ok(new { Message = "Consumer group updated successfully" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating consumer group {ConsumerGroupId}", consumerGroupId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpDelete("consumer-groups/{consumerGroupId}")]
  public async Task<ActionResult> DeactivateConsumerGroup(int consumerGroupId)
  {
    try
    {
      var success = await _topicRegistrationService.DeactivateConsumerGroupAsync(consumerGroupId);

      if (!success)
      {
        return NotFound($"Consumer group with ID {consumerGroupId} not found");
      }

      _logger.LogInformation("Consumer group {ConsumerGroupId} deactivated successfully", consumerGroupId);
      return Ok(new { Message = "Consumer group deactivated successfully" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating consumer group {ConsumerGroupId}", consumerGroupId);
      return StatusCode(500, "Internal server error");
    }
  }
}
