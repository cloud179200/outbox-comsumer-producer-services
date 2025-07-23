// Backward compatibility - imports from organized model files
global using ConsumerService.Models.Core;
global using ConsumerService.Models.Messages;
global using ConsumerService.Models.DTOs;
global using ConsumerService.Models.Enums;

namespace ConsumerService.Models;

// This file maintains backward compatibility while the codebase is organized.
// All model classes have been moved to more specific namespace files for better readability:
// 
// 📁 Messages/ - Message-related classes
//   - OutboxMessage.cs
//   - ConsumerMessage.cs
//   - ProcessedMessage.cs
//   - FailedMessage.cs
//
// 📁 DTOs/ - Data Transfer Objects
//   - AcknowledgmentRequest.cs
//   - ConsumerGroupConfig.cs
//   - AgentRegistrationRequest.cs
//   
// 📁 Enums/ - Enumeration types
//   - OutboxMessageStatus.cs
//   - ServiceType.cs
//   - HealthStatus.cs

// Re-export main classes for backward compatibility
public class OutboxModels
{
  // ✅ MESSAGE CLASSES (ConsumerService.Models.Messages.*)
  // - OutboxMessage: Reliable message delivery tracking
  // - ConsumerMessage: Incoming message for processing
  // - ProcessedMessage: Successfully processed message record
  // - FailedMessage: Failed processing record with error details

  // ✅ DTO CLASSES (ConsumerService.Models.DTOs.*)
  // - AcknowledgmentRequest: Message processing acknowledgment
  // - ConsumerGroupConfig: Consumer group configuration
  // - AgentRegistrationRequest: Service registration data

  // ✅ ENUMERATIONS (ConsumerService.Models.Enums.*)
  // - OutboxMessageStatus: Message processing states
  // - ServiceType: Producer/Consumer type identification
  // - HealthStatus: Service health monitoring states
}
