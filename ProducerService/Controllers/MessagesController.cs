using Microsoft.AspNetCore.Mvc;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
  private readonly IOutboxService _outboxService;
  private readonly IQuartzMessageBatchingService _quartzBatchingService;
  private readonly ILogger<MessagesController> _logger;

  public MessagesController(IOutboxService outboxService, IQuartzMessageBatchingService quartzBatchingService, ILogger<MessagesController> logger)
  {
    _outboxService = outboxService;
    _quartzBatchingService = quartzBatchingService;
    _logger = logger;
  }

  [HttpGet("health")]
  public ActionResult<object> GetHealth()
  {
    return Ok(new
    {
      Status = "Healthy",
      Timestamp = DateTime.UtcNow,
      Service = "Producer Service"
    });
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

      if (request.UseBatching)
      {
        // Queue the message for batch processing
        var messageId = await _quartzBatchingService.QueueMessageAsync(request);

        _logger.LogDebug("Message {MessageId} queued for batch processing on topic {Topic}", messageId, request.Topic);

        // Return immediately with queued status
        return Ok(new MessageResponse
        {
          MessageId = messageId,
          Status = "Queued for batch processing",
          Topic = request.Topic,
          Timestamp = DateTime.UtcNow
        });
      }
      else
      {
        // Process immediately
        return await ProcessImmediateMessage(request);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing message for topic {Topic}", request.Topic);
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

  private async Task<ActionResult<MessageResponse>> ProcessImmediateMessage(MessageRequest request)
  {
    try
    {
      var messages = await _outboxService.CreateMessagesForTopicAsync(request.Topic, request.Message, request.ConsumerGroup);

      if (messages == null || !messages.Any())
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
        TargetConsumerGroups = messages.Select(m => m.ConsumerGroup).ToList(),
        ProducerServiceId = messages.First().ProducerServiceId,
        ProducerInstanceId = messages.First().ProducerInstanceId
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing immediate message for topic {Topic}", request.Topic);
      throw;
    }
  }
}
