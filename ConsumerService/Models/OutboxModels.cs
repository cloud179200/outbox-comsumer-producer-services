// Backward compatibility - imports from organized model files
global using ConsumerService.Models.Core;

namespace ConsumerService.Models;

// This file maintains backward compatibility while the codebase is organized
// All model classes have been moved to more specific namespace files:
// - Core models: ConsumerService.Models.Core

// Re-export main classes for backward compatibility
public class OutboxModels
{
  // Core models available via: ConsumerService.Models.Core
  // - OutboxMessage
  // - OutboxMessageStatus (enum)
  // - ConsumerMessage
  // - ProcessedMessage
  // - FailedMessage
  // - AcknowledgmentRequest
  // - ConsumerGroupConfig
  // - AgentRegistrationRequest
  // - ServiceType (enum)
  // - HealthStatus (enum)
}
