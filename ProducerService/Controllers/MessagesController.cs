using Microsoft.AspNetCore.Mvc;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
  private readonly IOutboxService _outboxService;
  private readonly ILogger<MessagesController> _logger;

  public MessagesController(IOutboxService outboxService, ILogger<MessagesController> logger)
  {
    _outboxService = outboxService;
    _logger = logger;
  }

  [HttpPost("send")]
  public async Task<ActionResult<MessageResponse>> SendMessage([FromBody] MessageRequest request)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(request.Topic) || string.IsNullOrWhiteSpace(request.Message))
      {
        return BadRequest("Topic and Message are required");
      }

      var messages = await _outboxService.CreateMessagesForTopicAsync(
          request.Topic,
          request.Message,
          request.ConsumerGroup);

      if (!messages.Any())
      {
        return BadRequest($"No registered consumer groups found for topic '{request.Topic}'. " +
            "Please register the topic and consumer groups first using /api/topics/register endpoint.");
      }

      _logger.LogInformation("Created {Count} outbox messages for topic {Topic}",
          messages.Count, request.Topic);

      return Ok(new MessageResponse
      {
        MessageId = messages.First().Id,
        Status = "Queued",
        Topic = request.Topic,
        TargetConsumerGroups = messages.Select(m => m.ConsumerGroup).ToList()
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending message to topic {Topic}", request.Topic);
      return StatusCode(500, "Internal server error");
    }
  }
  [HttpPost("acknowledge")]
  public async Task<ActionResult> AcknowledgeMessage([FromBody] AcknowledgmentRequest request)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(request.MessageId))
      {
        return BadRequest("MessageId is required");
      }

      var message = await _outboxService.GetMessageAsync(request.MessageId);
      if (message == null)
      {
        return NotFound($"Message {request.MessageId} not found");
      }

      var status = request.Success ? OutboxMessageStatus.Acknowledged : OutboxMessageStatus.Failed;
      var success = await _outboxService.UpdateMessageStatusAsync(request.MessageId, status, request.ErrorMessage);

      if (success)
      {
        _logger.LogInformation("Message {MessageId} acknowledged with status {Status}",
            request.MessageId, status);
        return Ok(new { Status = "Acknowledged" });
      }
      else
      {
        return StatusCode(500, "Failed to acknowledge message");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error acknowledging message {MessageId}", request.MessageId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("{messageId}/status")]
  public async Task<ActionResult<OutboxMessage>> GetMessageStatus(string messageId)
  {
    try
    {
      var message = await _outboxService.GetMessageAsync(messageId);
      if (message == null)
      {
        return NotFound($"Message {messageId} not found");
      }

      return Ok(message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting status for message {MessageId}", messageId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("pending")]
  public async Task<ActionResult<List<OutboxMessage>>> GetPendingMessages([FromQuery] int limit = 100)
  {
    try
    {
      var messages = await _outboxService.GetPendingMessagesAsync(limit);
      return Ok(messages);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting pending messages");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("consumer-group/{consumerGroup}")]
  public async Task<ActionResult<List<OutboxMessage>>> GetMessagesForConsumerGroup(string consumerGroup, [FromQuery] int limit = 100)
  {
    try
    {
      var messages = await _outboxService.GetMessagesForConsumerGroupAsync(consumerGroup, limit);
      return Ok(messages);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting messages for consumer group {ConsumerGroup}", consumerGroup);
      return StatusCode(500, "Internal server error");
    }
  }
}
