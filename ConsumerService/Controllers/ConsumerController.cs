using Microsoft.AspNetCore.Mvc;
using ConsumerService.Services;
using ConsumerService.Models;

namespace ConsumerService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsumerController : ControllerBase
{
  private readonly IConsumerTrackingService _consumerTrackingService;
  private readonly ILogger<ConsumerController> _logger;

  public ConsumerController(IConsumerTrackingService consumerTrackingService, ILogger<ConsumerController> logger)
  {
    _consumerTrackingService = consumerTrackingService;
    _logger = logger;
  }

  [HttpGet("health")]
  public ActionResult<object> GetHealth()
  {
    return Ok(new
    {
      Status = "Healthy",
      Timestamp = DateTime.UtcNow,
      Service = "Consumer Service"
    });
  }

  [HttpGet("processed/{consumerGroup}")]
  public async Task<ActionResult<List<ProcessedMessage>>> GetProcessedMessages(string consumerGroup, [FromQuery] int limit = 100)
  {
    try
    {
      var processedMessages = await _consumerTrackingService.GetProcessedMessagesAsync(consumerGroup, limit);
      return Ok(processedMessages);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting processed messages for consumer group {ConsumerGroup}", consumerGroup);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("failed/{consumerGroup}")]
  public async Task<ActionResult<List<FailedMessage>>> GetFailedMessages(string consumerGroup, [FromQuery] int limit = 100)
  {
    try
    {
      var failedMessages = await _consumerTrackingService.GetFailedMessagesAsync(consumerGroup, limit);
      return Ok(failedMessages);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting failed messages for consumer group {ConsumerGroup}", consumerGroup);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPost("test-process")]
  public async Task<ActionResult> TestProcessMessage([FromBody] ConsumerMessage message)
  {
    try
    {
      using var scope = HttpContext.RequestServices.CreateScope();
      var messageProcessor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();

      var success = await messageProcessor.ProcessMessageAsync(message);

      if (success)
      {
        return Ok(new { Status = "Processed", MessageId = message.MessageId });
      }
      else
      {
        return BadRequest(new { Status = "Failed", MessageId = message.MessageId });
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error testing message processing for message {MessageId}", message.MessageId);
      return StatusCode(500, "Internal server error");
    }
  }
}
