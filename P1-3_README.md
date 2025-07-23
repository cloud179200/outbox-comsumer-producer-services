# Outbox Pattern with PostgreSQL and Kafka (Docker-Based)

> **📋 System Status**: [FULLY OPERATIONAL](SYSTEM-FULLY-OPERATIONAL.md) - All producers and consumers are healthy and working correctly.

This project implements the **Transactional Outbox Pattern** using PostgreSQL for reliable message delivery between producer and consumer services with Kafka as the message broker. The entire system is containerized using Docker for easy deployment and horizontal scaling.

## 🏗️ System Architecture

The solution consists of two primary ASP.NET Core services with comprehensive data flow management:

### 📤 Producer Service (Outbox Pattern Implementation)
- **Message API**: REST endpoints for message publishing with batching support
- **Outbox Storage**: PostgreSQL-based outbox table with message state tracking
- **Background Jobs**: Quartz.NET scheduled jobs for processing and retry management
- **Agent Management**: Service registration and health monitoring for horizontal scaling
- **Message Batching**: Configurable batching system for high-throughput scenarios

### 📥 Consumer Service (Reliable Processing)
- **Kafka Consumer**: Multi-group consumer with topic-based message routing
- **Idempotency**: Duplicate prevention using message IDs and idempotency keys
- **Processing Tracking**: PostgreSQL tracking of processed and failed messages
- **Acknowledgment System**: Producer notification with success/failure reporting
- **Agent Registration**: Auto-registration with producer services for scaling

## 🔄 Data Flow and Process Workflow

### 1. Message Publication Flow
```
Client Request → Producer API → Message Batching → Outbox Storage → Kafka Publishing → Consumer Processing → Acknowledgment
```

**Detailed Steps:**
1. **Client Request**: Application sends message via REST API
2. **Message Batching**: Messages queued for batch processing (configurable)
3. **Outbox Storage**: Messages stored in PostgreSQL with PENDING status
4. **Topic Resolution**: System resolves target consumer groups from topic registration
5. **Kafka Publishing**: Background job sends messages to Kafka topics
6. **Status Updates**: Message status updated to SENT in outbox table

### 2. Message Processing Flow
```
Kafka Topic → Consumer Service → Idempotency Check → Business Processing → Acknowledgment → Producer Notification
```

**Detailed Steps:**
1. **Kafka Consumption**: Consumer polls messages from assigned topics
2. **Idempotency Check**: Verify message hasn't been processed (by ID + idempotency key)
3. **Business Processing**: Execute domain-specific message processing logic
4. **State Persistence**: Mark message as processed in consumer tracking table
5. **Acknowledgment**: Send success/failure notification to producer service
6. **Outbox Update**: Producer updates message status to ACKNOWLEDGED/FAILED

### 3. Retry and Recovery Flow
```
Failed Message → Retry Job → Consumer Group Check → Targeted Retry → Kafka Republish → Processing Attempt
```

**Detailed Steps:**
1. **Failure Detection**: Messages not acknowledged within timeout period
2. **Retry Logic**: ProcessRetryMessagesJob identifies unacknowledged messages
3. **Retry Strategy**: Configurable retry count (supports infinite retry with MaxRetries = -1)
4. **Targeted Retry**: Messages can be targeted to specific consumer instances
5. **Exponential Backoff**: Built-in retry delays to prevent system overload

## 🗄️ Database Schema and Models

### Producer Service Database (PostgreSQL)

**OutboxMessage Table** - Core outbox pattern implementation:
```sql
-- Message lifecycle tracking
Id (Primary), Topic, Message, CreatedAt, Status, ProcessedAt
ConsumerGroup, TopicRegistrationId, RetryCount, LastRetryAt
ProducerServiceId, ProducerInstanceId, ErrorMessage

-- Retry and targeting support  
IsRetry, TargetConsumerServiceId, OriginalMessageId
ScheduledRetryAt, IdempotencyKey
```

**TopicRegistration Table** - Topic and consumer group configuration:
```sql
-- Topic configuration
Id, TopicName, Description, IsActive, CreatedAt, UpdatedAt

-- Consumer group relationships
ConsumerGroupRegistrations (1:Many relationship)
```

**ConsumerGroupRegistration Table** - Consumer group settings:
```sql
-- Consumer group configuration
Id, ConsumerGroupName, TopicRegistrationId, RequiresAcknowledgment
IsActive, AcknowledgmentTimeoutMinutes, MaxRetries, CreatedAt

-- Navigation properties
TopicRegistration (Foreign Key), ConsumerAcknowledgments (1:Many)
```

**Service Agent Tables** - Horizontal scaling support:
```sql
-- ProducerServiceAgent: Tracks producer instances
-- ConsumerServiceAgent: Tracks consumer instances  
-- ServiceHealthCheck: Health monitoring data
```

### Consumer Service Database (PostgreSQL)

**ProcessedMessage Table** - Idempotency tracking:
```sql
-- Primary key: (MessageId, ConsumerGroup)
MessageId, ConsumerGroup, Topic, ProcessedAt, Content
ProducerServiceId, ProducerInstanceId, ConsumerServiceId
ConsumerInstanceId, IdempotencyKey
```

**FailedMessage Table** - Failure tracking:
```sql
-- Failed message details
Id, MessageId, ConsumerGroup, Topic, ErrorMessage, FailedAt
ProducerServiceId, ProducerInstanceId, ConsumerServiceId
```

## ⚙️ Configuration and Setup

### Environment Variables

**Producer Service Configuration:**
```bash
# Database connection
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=outbox_db;Username=outbox_user;Password=outbox_password"

# Kafka connection  
ConnectionStrings__Kafka="localhost:9092"

# Service identification
SERVICE_ID="producer-service-1"
INSTANCE_ID="producer-instance-1"
ASPNETCORE_ENVIRONMENT="Development"
```

**Consumer Service Configuration:**
```bash
# Database connection
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=outbox_db;Username=outbox_user;Password=outbox_password"

# Service identification
SERVICE_ID="consumer-service-1" 
INSTANCE_ID="consumer-instance-1"

# Consumer group assignment (Docker environment)
KAFKA_CONSUMER_GROUP="group-a"
KAFKA_TOPICS="shared-events,user-events"

# Producer service registration
ProducerService__BaseUrl="http://localhost:5299"
```

### Topic and Consumer Group Setup

**Default Seeded Topics:**
- `user-events` → `default-consumer-group`
- `order-events` → `default-consumer-group`, `inventory-service`  
- `analytics-events` → `analytics-group`
- `notification-events` → `notification-group`, `email-service`

**Consumer Group Configuration:**
```json
{
  "ConsumerGroups": [
    {
      "GroupName": "group-a",
      "Topics": ["user-events", "order-events"]
    },
    {
      "GroupName": "analytics-group", 
      "Topics": ["analytics-events"]
    }
  ]
}
```

### Background Job Configuration

**Quartz.NET Job Schedule:**
- **MessageBatchingJob**: 30 seconds (flushes pending batches)
- **ProcessPendingMessagesJob**: 5 seconds (sends messages to Kafka)
- **ProcessRetryMessagesJob**: 10 seconds (handles failed messages)
- **AgentHeartbeatJob**: 30 seconds (service health reporting)
- **CleanupOldMessagesJob**: Daily (removes old acknowledged messages)

## 🔧 Operating the System

### 1. Initial Setup and Configuration

**Step 1: Topic Registration**
```bash
# Register a new topic with consumer groups
curl -X POST "http://localhost:5301/api/topics/register" \
  -H "Content-Type: application/json" \
  -d '{
    "topicName": "payment-events",
    "description": "Payment processing events",
    "consumerGroups": [
      {
        "consumerGroupName": "payment-processor",
        "requiresAcknowledgment": true,
        "acknowledgmentTimeoutMinutes": 5,
        "maxRetries": 3
      }
    ]
  }'
```

**Step 2: Configure Consumer Groups**
```bash
# Update consumer group settings (enable infinite retry)
curl -X PUT "http://localhost:5301/api/topics/consumer-groups/1" \
  -H "Content-Type: application/json" \
  -d '{
    "consumerGroupName": "payment-processor",
    "requiresAcknowledgment": true,
    "acknowledgmentTimeoutMinutes": 5,
    "maxRetries": -1
  }'
```

### 2. Message Publishing

**Batch Processing (Default):**
```bash
curl -X POST "http://localhost:5301/api/messages/send" \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "user-events",
    "message": "User created: john.doe@example.com",
    "useBatching": true
  }'
```

**Immediate Processing:**
```bash
curl -X POST "http://localhost:5301/api/messages/send" \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "user-events", 
    "message": "Critical: Account locked",
    "useBatching": false
  }'
```

### 3. Consumer Implementation

**Custom Message Processor Example:**
```csharp
public class CustomMessageProcessor : IMessageProcessor
{
    public async Task<bool> ProcessMessageAsync(ConsumerMessage message)
    {
        try
        {
            // Business logic based on topic
            switch (message.Topic)
            {
                case "user-events":
                    await ProcessUserEvent(message.Content);
                    break;
                case "order-events":
                    await ProcessOrderEvent(message.Content);
                    break;
                default:
                    _logger.LogWarning("Unknown topic: {Topic}", message.Topic);
                    break;
            }
            
            return true; // Success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
            return false; // Will trigger retry
        }
    }
}
```

### 4. Monitoring and Troubleshooting

**Health Check Endpoints:**
```bash
# Producer service health
curl "http://localhost:5301/api/messages/health"

# Consumer service health  
curl "http://localhost:5401/api/consumer/health"

# Check processed messages for a consumer group
curl "http://localhost:5401/api/consumer/processed/group-a"
```

**Message Status Monitoring:**
```bash
# Check outbox message status
curl "http://localhost:5301/api/messages/pending"
curl "http://localhost:5301/api/messages/consumer-group/group-a"
```

**Verification Script:**
```powershell
# Run comprehensive acknowledgment verification
.\P2-3_verify-acknowledgments.ps1
```

### 5. Scaling Operations

**Horizontal Scaling with Docker:**
```bash
# Scale producers and consumers
docker-compose up -d --scale producer1=3 --scale consumer1=6

# Check service registration
curl "http://localhost:5301/api/agents/producers"
curl "http://localhost:5301/api/agents/consumers"
```

### 6. Common Troubleshooting

**Issue: Messages stuck in PENDING status**
- Check if ProcessPendingMessagesJob is running
- Verify Kafka connectivity
- Check producer service logs

**Issue: Messages never acknowledged** 
- Verify consumer group configuration
- Check consumer service health endpoints
- Verify acknowledgment timeout settings

**Issue: Duplicate message processing**
- Check idempotency key implementation
- Verify ProcessedMessage table constraints
- Review consumer message processing logic

**Issue: Infinite retry loop**
- Check MaxRetries configuration (-1 = infinite)
- Verify message processing logic doesn't always fail
- Monitor failed message patterns

## 🔍 Side Effects and Considerations

### Performance Impact
- **Database Load**: Outbox pattern increases database writes (1 message = 1+ outbox entries)
- **Kafka Load**: Messages published after database commit (eventual consistency)
- **Memory Usage**: Message batching requires in-memory queuing
- **Processing Latency**: Additional hop through outbox table adds ~50-100ms delay

### Consistency Guarantees
- **At-Least-Once Delivery**: Messages may be delivered multiple times (idempotency required)
- **Eventual Consistency**: Small delay between database commit and message publishing
- **Ordering**: Messages processed in creation order within same consumer group
- **Durability**: Messages persisted in database before publishing to Kafka

### Operational Overhead
- **Monitoring**: Requires monitoring of background jobs and message states
- **Cleanup**: Old messages need periodic cleanup (CleanupOldMessagesJob)
- **Scaling**: Consumer group assignment affects message distribution
- **Failure Handling**: Failed messages need manual intervention or infinite retry

### Data Retention
- **Outbox Messages**: Cleaned up after 7 days (configurable)
- **Failed Messages**: Manual cleanup required
- **Health Checks**: Cleaned up after 24 hours

## ✨ Key Features

✅ **Transactional Outbox Pattern**: Ensures message delivery reliability with database transactions  
✅ **Message Batching**: Configurable batching for high-throughput scenarios (500 messages/batch)  
✅ **Idempotency Guarantees**: Prevents duplicate processing using message IDs and idempotency keys  
✅ **Intelligent Retry Logic**: Configurable retry with support for infinite retry (MaxRetries = -1)  
✅ **Multi-Topic Support**: Topic registration system with consumer group management  
✅ **Horizontal Scaling**: Docker-based auto-scaling with service agent registration  
✅ **Health Monitoring**: Comprehensive health checks and heartbeat tracking  
✅ **Background Job Processing**: Quartz.NET scheduled jobs for reliable message processing  
✅ **PostgreSQL Integration**: Robust data persistence with proper indexing and relationships  
✅ **Kafka Integration**: High-performance message broker with topic partitioning  
✅ **Monitoring Dashboard**: Kafka UI for real-time system monitoring  
✅ **Failure Recovery**: Automatic retry with targeted consumer routing  
✅ **Agent Management**: Service discovery and registration for distributed systems  
✅ **Message Status Tracking**: Complete message lifecycle visibility  
✅ **Docker-First**: Full containerization with docker-compose orchestration

## Prerequisites

- Docker Desktop
- PowerShell (for management scripts)

## Quick Start

### 1. Start the Complete System

Start the entire system with scaled instances:

```powershell
.\P1-1_docker-manager.ps1
```

This will start:
- PostgreSQL (port 5432)
- Kafka (port 9092)
- Zookeeper (port 2181)
- Kafka UI (http://localhost:8080)
- 3 Producer Service instances (ports 5301, 5302, 5303)
- 6 Consumer Service instances (ports 5401, 5402, 5403, 5404, 5405, 5406)

### 2. Simple Start (Infrastructure Only)

For development or testing with local services:

```powershell
.\P1-2_docker-simple.ps1
```

This starts only the infrastructure (PostgreSQL, Kafka, Zookeeper, Kafka-UI).
### 3. Verify Setup

Check system health:

```powershell
.\docker-test.ps1
```

This will test all running services and show their health status.

### 4. Stop the System

```powershell
.\P1-1_docker-manager.ps1
# Then select option 9 to stop all services
```

## Docker Management Scripts

### P1-1_docker-manager.ps1

Primary management script for the scaled Docker system:

- Start/Stop complete system with multiple instances
- Health monitoring and logging
- Scaling configuration

### P1-2_docker-simple.ps1

Simple infrastructure management:

- Start/Stop infrastructure only (PostgreSQL, Kafka, Zookeeper, Kafka-UI)
- Useful for development with local services

### P2-8_cleanup.ps1

System testing and validation:

- Health checks for all services
- Database connectivity tests
- Kafka topic verification

## Usage Examples

### Send a Message

```bash
curl -X POST "http://localhost:5299/api/messages/send" \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "user-events",
    "message": "User John Doe created account",
    "consumerGroup": "consumer-group-1"
  }'
```

### Check System Health

```bash
curl "http://localhost:5299/api/messages/health"
curl "http://localhost:5287/api/consumer/health"
```

### Monitor with Kafka UI

Visit http://localhost:8080 to:
- View topics and partitions
- Monitor consumer groups
- See message flow and lag

## Configuration

The system uses PostgreSQL for persistent storage and supports multiple consumer groups with different topic subscriptions.

### Environment Variables

Services automatically detect their environment:
- `ASPNETCORE_ENVIRONMENT=Development` (default)
- `ASPNETCORE_ENVIRONMENT=Production` (for production deployment)

### Database Configuration

PostgreSQL connection strings are configured through environment variables:
- `ConnectionStrings__DefaultConnection` for Producer Service
- `ConnectionStrings__ConsumerConnection` for Consumer Service

## Scaling

The system supports horizontal scaling through Docker:

```powershell
# Scale to 5 producers and 10 consumers
docker-compose up -d --scale producer1=5 --scale consumer1=10
```

Consumer instances automatically register with different consumer groups to ensure load distribution.

## Monitoring

### Kafka UI (http://localhost:8080)
- View topics and partitions
- Monitor consumer groups
- See message flow and performance metrics

### Database Monitoring
- PostgreSQL accessible on port 5432
- Use your preferred database management tool
- Monitor outbox tables and message states

### Service Logs
```powershell
# View all service logs
docker-compose logs -f

# View specific service logs
docker-compose logs -f outbox-producer1
docker-compose logs -f outbox-consumer1
```

## Development

### Building Docker Images

Images are built automatically by docker-compose, but you can build manually:

```powershell
# Build both services
docker-compose build

# Build specific service
docker-compose build producer1
docker-compose build consumer1
```

### Local Development

For local development, you can run infrastructure only and develop services locally:

```powershell
# Start infrastructure
```powershell
.\P1-2_docker-simple.ps1 -Action Start
```

# Run services locally with dotnet run
cd ProducerService
dotnet run --urls "http://localhost:5299"

cd ConsumerService  
dotnet run --urls "http://localhost:5287"
```

## Production Considerations

- Configure appropriate PostgreSQL persistence and backup strategies
- Set up Kafka cluster with replication for high availability
- Implement proper monitoring and alerting
- Configure security (authentication/authorization)
- Set up load balancing for multiple service instances
- Configure appropriate retry policies and timeouts
- Use Docker Swarm or Kubernetes for orchestration
- Set up proper logging and log aggregation
- Configure health checks and service discovery

## 🔍 Troubleshooting

### Common Issues

1. **Services Not Starting**
   ```bash
   # Check container logs
   docker logs outbox-producer1
   docker logs outbox-consumer1
   
   # Check database connectivity
   docker logs outbox-postgres
   ```

2. **Messages Not Being Processed**
   ```bash
   # Check Kafka logs
   docker logs outbox-kafka
   
   # Verify topic creation
   docker exec -it outbox-kafka kafka-topics.sh --list --bootstrap-server localhost:9092
   ```

3. **Consumer Group Issues**
   ```bash
   # Check consumer group status
   curl http://localhost:5301/api/topics/consumer-groups
   
   # Verify acknowledgments
   .\P2-3_verify-acknowledgments.ps1
   ```

### Log Analysis

- **Producer Logs**: Message creation, batching, Kafka publishing
- **Consumer Logs**: Message processing, acknowledgments, failures
- **Database Logs**: Connection issues, query performance
- **Kafka Logs**: Topic management, partition distribution

### Performance Issues

- Monitor batch processing efficiency
- Check database connection pools
- Verify Kafka partition distribution
- Review retry patterns and infinite loops

## 📊 Monitoring & Verification

### Message Processing Verification

The system includes comprehensive monitoring scripts to verify message processing across all consumer groups:

#### 1. Acknowledgment Verification Script

```powershell
# Run the comprehensive verification
.\P2-3_verify-acknowledgments.ps1

# This script checks:
# - Message processing status for each consumer group (group-a, group-b, group-c)
# - Failed message counts and error details
# - Producer outbox status across all producers
# - Message status distribution (Pending, Sent, Acknowledged, Failed, Expired)
```

#### 2. End-to-End Testing

```powershell
# Comprehensive E2E test with infinite retry
.\P2-1_e2e-comprehensive-test.ps1 -MessageCount 10 -UseBatching $true

# Session-specific testing (isolates test messages)
.\P2-2_run-e2e-test.ps1
```

#### 3. SQL Verification Queries

```sql
-- Check latest acknowledgments
SELECT "MessageId", "ConsumerGroupRegistrationId", "Success", "AcknowledgedAt", "ErrorMessage"
FROM "ConsumerAcknowledgments"
ORDER BY "AcknowledgedAt" DESC LIMIT 10;

-- Check outbox message status by consumer group
SELECT om."ConsumerGroup", om."Status", COUNT(*) as "Count"
FROM "OutboxMessages" om
GROUP BY om."ConsumerGroup", om."Status"
ORDER BY om."ConsumerGroup", om."Status";
```

### Real-time Monitoring Endpoints

#### Consumer Services
```bash
# Check processed messages for specific consumer group
curl http://localhost:5401/api/consumer/processed/group-a

# Check failed messages
curl http://localhost:5401/api/consumer/failed/group-a

# Health check
curl http://localhost:5401/health
```

#### Producer Services
```bash
# Check pending messages
curl http://localhost:5301/api/messages/pending

# Check messages by consumer group
curl http://localhost:5301/api/messages/consumer-group/group-a

# View all consumer groups
curl http://localhost:5301/api/topics/consumer-groups
```

### Message Flow Verification

The verification scripts ensure:

1. **All Consumer Groups Receive Messages**: Each message is delivered to all registered consumer groups (group-a, group-b, group-c)
2. **Acknowledgment Tracking**: Each consumer properly acknowledges message processing
3. **Failure Handling**: Failed messages are tracked and retried infinitely (when MaxRetries = -1)
4. **Horizontal Scaling**: Multiple consumers within each group process messages correctly
5. **Idempotency**: Duplicate messages are properly handled using idempotency keys

### Key Verification Points

- ✅ **Service Health**: All producers and consumers are responding
- ✅ **Message Delivery**: Messages reach all registered consumer groups
- ✅ **Acknowledgment Processing**: Consumers properly acknowledge success/failure
- ✅ **Infinite Retry**: Failed messages are retried indefinitely
- ✅ **Batching vs Immediate**: Both processing modes work correctly
- ✅ **Consumer Group Independence**: Each group processes all messages independently
