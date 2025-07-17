using Microsoft.AspNetCore.Mvc;
using ProducerService.Models;
using ProducerService.Services;

namespace ProducerService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
  private readonly IAgentService _agentService;
  private readonly ILogger<AgentsController> _logger;

  public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
  {
    _agentService = agentService;
    _logger = logger;
  }

  // Producer Agent Management Endpoints
  [HttpPost("producers/register")]
  public async Task<ActionResult<AgentResponse>> RegisterProducerAgent([FromBody] AgentRegistrationRequest request)
  {
    try
    {
      var result = await _agentService.RegisterProducerAgentAsync(request);
      _logger.LogInformation("Producer agent {ServiceId} registered successfully", request.ServiceId);
      return Ok(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error registering producer agent {ServiceId}", request.ServiceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPost("producers/heartbeat")]
  public async Task<ActionResult> UpdateProducerHeartbeat([FromBody] AgentHeartbeatRequest request)
  {
    try
    {
      var success = await _agentService.UpdateProducerHeartbeatAsync(request);
      if (success)
      {
        return Ok(new { Status = "Heartbeat updated" });
      }
      return NotFound($"Producer agent {request.ServiceId} not found");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating producer heartbeat for {ServiceId}", request.ServiceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("producers")]
  public async Task<ActionResult<List<AgentResponse>>> GetActiveProducerAgents()
  {
    try
    {
      var agents = await _agentService.GetActiveProducerAgentsAsync();
      return Ok(agents);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting active producer agents");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("producers/{serviceId}")]
  public async Task<ActionResult<AgentResponse>> GetProducerAgent(string serviceId)
  {
    try
    {
      var agent = await _agentService.GetProducerAgentAsync(serviceId);
      if (agent == null)
      {
        return NotFound($"Producer agent {serviceId} not found");
      }
      return Ok(agent);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting producer agent {ServiceId}", serviceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpDelete("producers/{serviceId}")]
  public async Task<ActionResult> DeactivateProducerAgent(string serviceId)
  {
    try
    {
      var success = await _agentService.DeactivateProducerAgentAsync(serviceId);
      if (success)
      {
        return Ok(new { Status = "Agent deactivated" });
      }
      return NotFound($"Producer agent {serviceId} not found");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating producer agent {ServiceId}", serviceId);
      return StatusCode(500, "Internal server error");
    }
  }

  // Consumer Agent Management Endpoints
  [HttpPost("consumers/register")]
  public async Task<ActionResult<AgentResponse>> RegisterConsumerAgent([FromBody] AgentRegistrationRequest request)
  {
    try
    {
      var result = await _agentService.RegisterConsumerAgentAsync(request);
      _logger.LogInformation("Consumer agent {ServiceId} registered successfully", request.ServiceId);
      return Ok(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error registering consumer agent {ServiceId}", request.ServiceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPost("consumers/heartbeat")]
  public async Task<ActionResult> UpdateConsumerHeartbeat([FromBody] AgentHeartbeatRequest request)
  {
    try
    {
      var success = await _agentService.UpdateConsumerHeartbeatAsync(request);
      if (success)
      {
        return Ok(new { Status = "Heartbeat updated" });
      }
      return NotFound($"Consumer agent {request.ServiceId} not found");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating consumer heartbeat for {ServiceId}", request.ServiceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("consumers")]
  public async Task<ActionResult<List<AgentResponse>>> GetActiveConsumerAgents()
  {
    try
    {
      var agents = await _agentService.GetActiveConsumerAgentsAsync();
      return Ok(agents);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting active consumer agents");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("consumers/{serviceId}")]
  public async Task<ActionResult<AgentResponse>> GetConsumerAgent(string serviceId)
  {
    try
    {
      var agent = await _agentService.GetConsumerAgentAsync(serviceId);
      if (agent == null)
      {
        return NotFound($"Consumer agent {serviceId} not found");
      }
      return Ok(agent);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting consumer agent {ServiceId}", serviceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpDelete("consumers/{serviceId}")]
  public async Task<ActionResult> DeactivateConsumerAgent(string serviceId)
  {
    try
    {
      var success = await _agentService.DeactivateConsumerAgentAsync(serviceId);
      if (success)
      {
        return Ok(new { Status = "Agent deactivated" });
      }
      return NotFound($"Consumer agent {serviceId} not found");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deactivating consumer agent {ServiceId}", serviceId);
      return StatusCode(500, "Internal server error");
    }
  }

  // Service Discovery Endpoints
  [HttpGet("discover")]
  public async Task<ActionResult<List<AgentResponse>>> DiscoverServices([FromQuery] ServiceType? serviceType = null)
  {
    try
    {
      var services = await _agentService.DiscoverServicesAsync(serviceType);
      return Ok(services);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error discovering services");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("producers/healthiest")]
  public async Task<ActionResult<AgentResponse>> GetHealthiestProducer()
  {
    try
    {
      var producer = await _agentService.GetHealthiestProducerAsync();
      if (producer == null)
      {
        return NotFound("No healthy producer agents found");
      }
      return Ok(producer);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting healthiest producer");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("producers/least-loaded")]
  public async Task<ActionResult<AgentResponse>> GetLeastLoadedProducer()
  {
    try
    {
      var producer = await _agentService.GetLeastLoadedProducerAsync();
      if (producer == null)
      {
        return NotFound("No producer agents found");
      }
      return Ok(producer);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting least loaded producer");
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("consumers/for-group/{consumerGroup}")]
  public async Task<ActionResult<List<AgentResponse>>> GetHealthyConsumersForGroup(string consumerGroup)
  {
    try
    {
      var consumers = await _agentService.GetHealthyConsumersForGroupAsync(consumerGroup);
      return Ok(consumers);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting healthy consumers for group {ConsumerGroup}", consumerGroup);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpGet("consumers/for-topic/{topic}")]
  public async Task<ActionResult<AgentResponse>> GetBestConsumerForTopic(string topic)
  {
    try
    {
      var consumer = await _agentService.GetBestConsumerForTopicAsync(topic);
      if (consumer == null)
      {
        return NotFound($"No consumer agents found for topic {topic}");
      }
      return Ok(consumer);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting best consumer for topic {Topic}", topic);
      return StatusCode(500, "Internal server error");
    }
  }

  // Health Monitoring Endpoints
  [HttpPost("health-check/{serviceId}")]
  public async Task<ActionResult> PerformHealthCheck(string serviceId, [FromQuery] ServiceType serviceType)
  {
    try
    {
      await _agentService.PerformHealthCheckAsync(serviceId, serviceType);
      return Ok(new { Status = "Health check performed" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error performing health check for {ServiceType} {ServiceId}", serviceType, serviceId);
      return StatusCode(500, "Internal server error");
    }
  }

  [HttpPost("cleanup")]
  public async Task<ActionResult> CleanupInactiveAgents()
  {
    try
    {
      await _agentService.CleanupInactiveAgentsAsync();
      return Ok(new { Status = "Cleanup completed" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during agent cleanup");
      return StatusCode(500, "Internal server error");
    }
  }

  // Current Service Information
  [HttpGet("current")]
  public ActionResult<object> GetCurrentServiceInfo()
  {
    try
    {
      var agentService = _agentService as AgentService;
      return Ok(new
      {
        ServiceId = agentService?.GetCurrentServiceId() ?? "unknown",
        InstanceId = agentService?.GetCurrentInstanceId() ?? "unknown",
        ServiceType = ServiceType.Producer,
        Timestamp = DateTime.UtcNow
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting current service info");
      return StatusCode(500, "Internal server error");
    }
  }
}