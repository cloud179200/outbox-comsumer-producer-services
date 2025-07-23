// Backward compatibility - imports from organized model files
global using ProducerService.Models.Core;
global using ProducerService.Models.Agents;
global using ProducerService.Models.DTOs;

namespace ProducerService.Models;

// This file maintains backward compatibility while the codebase is organized
// All model classes have been moved to more specific namespace files:
// - Core models: ProducerService.Models.Core
// - Agent models: ProducerService.Models.Agents  
// - DTO models: ProducerService.Models.DTOs

// Re-export main classes for backward compatibility
public class OutboxModels
{
  // Core models available via: ProducerService.Models.Core
  // - OutboxMessage
  // - OutboxMessageStatus (enum)
  // - TopicRegistration
  // - ConsumerGroupRegistration
  // - ConsumerAcknowledgment

  // Agent models available via: ProducerService.Models.Agents
  // - ProducerServiceAgent
  // - ConsumerServiceAgent
  // - ServiceHealthCheck
  // - AgentStatus (enum)
  // - ServiceType (enum)
  // - HealthStatus (enum)

  // DTO models available via: ProducerService.Models.DTOs
  // - MessageRequest/MessageResponse
  // - AcknowledgmentRequest
  // - TopicRegistrationRequest/TopicRegistrationResponse
  // - ConsumerGroupRequest/ConsumerGroupResponse
  // - AgentRegistrationRequest/AgentHeartbeatRequest/AgentResponse
}
