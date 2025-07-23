using Microsoft.AspNetCore.Mvc;
using ConsumerService.Services;
using ConsumerService.Models;

namespace ConsumerService.Controllers;

/// <summary>
/// Consumer service controller providing endpoints for message tracking, health monitoring, and testing.
/// Manages consumer group message processing status and provides operational insights.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConsumerController : ControllerBase
{
  private readonly IConsumerTrackingService _consumerTrackingService;
  private readonly ILogger<ConsumerController> _logger;

  /// <summary>
  /// Initializes the Consumer Controller with required tracking services.
  /// Sets up message tracking and logging for consumer operations monitoring.
  /// </summary>
  /// <param name="consumerTrackingService">Service for tracking processed and failed messages</param>
  /// <param name="logger">Logger for tracking consumer operations</param>
  public ConsumerController(IConsumerTrackingService consumerTrackingService, ILogger<ConsumerController> logger)
  {
    _consumerTrackingService = consumerTrackingService;
    _logger = logger;
  }

  /// <summary>
  /// Health check endpoint to verify consumer service availability and status.
  /// Used by load balancers and monitoring systems to determine service health.
  /// </summary>
  /// <returns>Health status object with timestamp and service identification</returns>
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

  /// <summary>
  /// Retrieves processed messages for a specific consumer group with optional limit.
  /// Provides visibility into successfully processed messages for monitoring and debugging.
  /// </summary>
  /// <param name="consumerGroup">The consumer group name to retrieve processed messages for</param>
  /// <param name="limit">Maximum number of messages to return (default: 100)</param>
  /// <returns>List of processed messages with processing details and timestamps</returns>
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
