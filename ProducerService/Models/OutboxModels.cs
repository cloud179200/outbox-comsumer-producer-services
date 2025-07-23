// Backward compatibility - imports from organized model files
global using ProducerService.Models.Core;
global using ProducerService.Models.Agents;
global using ProducerService.Models.DTOs;
global using ProducerService.Models.Messages;
global using ProducerService.Models.Enums;

namespace ProducerService.Models;

// This file maintains backward compatibility while the codebase is organized
// All model classes have been moved to more specific namespace files:
// - Core models: ProducerService.Models.Core (Topic/Consumer registrations)
// - Agent models: ProducerService.Models.Agents (Service agents, health checks)
// - DTO models: ProducerService.Models.DTOs (API request/response models)
// - Message models: ProducerService.Models.Messages (OutboxMessage)
// - Enum models: ProducerService.Models.Enums (All enums)

/// <summary>
/// Compatibility class that re-exports all model types for backward compatibility.
/// For new development, use the specific namespace imports directly.
/// </summary>
public class OutboxModels
{
  // Core models available via: ProducerService.Models.Core
  // - TopicRegistration
  // - ConsumerGroupRegistration  
  // - ConsumerAcknowledgment

  // Agent models available via: ProducerService.Models.Agents
  // - ProducerServiceAgent
  // - ConsumerServiceAgent
  // - ServiceHealthCheck

  // DTO models available via: ProducerService.Models.DTOs
  // - MessageRequest/MessageResponse
  // - AcknowledgmentRequest
  // - TopicRegistrationRequest/TopicRegistrationResponse
  // - ConsumerGroupRequest/ConsumerGroupResponse
  // - AgentRegistrationRequest/AgentHeartbeatRequest/AgentResponse

  // Message models available via: ProducerService.Models.Messages
  // - OutboxMessage

  // Enum models available via: ProducerService.Models.Enums
  // - OutboxMessageStatus
  // - AgentStatus
  // - ServiceType
  // - HealthStatus
}
