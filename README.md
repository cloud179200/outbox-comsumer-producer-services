# Outbox Pattern with Redis and Kafka

This project implements the Outbox Pattern using Redis for reliable message delivery between producer and consumer services with Kafka as the message broker.

## Architecture

The solution consists of two ASP.NET Core services:

### Producer Service (Port 5299)
- **API for Message Publishing**: Receives messages and topic information
- **Outbox Storage**: Stores messages in Redis with tracking information
- **Background Processing**: Continuously processes pending messages and sends them to Kafka
- **Acknowledgment Handling**: Tracks consumer acknowledgments and handles retries

### Consumer Service (Port 5287)
- **Kafka Consumer**: Consumes messages from multiple topics with different consumer groups
- **Message Processing**: Processes messages with idempotency guarantees
- **Outbox Tracking**: Tracks processed messages to prevent duplicate processing
- **Acknowledgment**: Sends acknowledgments back to producer service

## Key Features

✅ **Reliable Message Delivery**: Outbox pattern ensures no message loss  
✅ **Idempotency**: Prevents duplicate message processing  
✅ **Retry Logic**: Automatic retry for failed messages  
✅ **Multiple Consumer Groups**: Support for different consumer groups and topics  
✅ **Monitoring**: Redis and Kafka UI for system monitoring  
✅ **Scalable**: Can run multiple instances of both services

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop
- Visual Studio 2022 or VS Code

## Quick Start

### 1. Start Infrastructure

Start Redis and Kafka using Docker Compose:

```powershell
docker-compose up -d
```

This will start:
- Redis (port 6379)
- Kafka (port 9092)
- Kafka UI (http://localhost:8080)
- Redis Commander (http://localhost:8081)

### 2. Run Services

#### Option A: Using Visual Studio
1. Open `OutboxPattern.sln`
2. Set multiple startup projects (ProducerService and ConsumerService)
3. Press F5 to run both services

#### Option B: Using Command Line

**Terminal 1 - Producer Service:**
```powershell
cd ProducerService
dotnet run
```

**Terminal 2 - Consumer Service:**
```powershell
cd ConsumerService
dotnet run
```

### 3. Verify Setup

Check service health:
- Producer Service: http://localhost:5299/api/messages/health
- Consumer Service: http://localhost:5287/api/consumer/health

## Usage Examples

### Send a Message

```bash
curl -X POST "http://localhost:5299/api/messages/send" \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "user-events",
    "message": "User John Doe created account",
    "consumerGroup": "default-consumer-group"
  }'
```

### Check Message Status

```bash
curl "http://localhost:5299/api/messages/status/{messageId}"
```

### Get Pending Messages

```bash
curl "http://localhost:5299/api/messages/pending"
```

### Get Processed Messages for Consumer Group

```bash
curl "http://localhost:5287/api/consumer/processed/default-consumer-group"
```

## Configuration

### Producer Service Settings

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "Kafka": "localhost:9092"
  },
  "OutboxProcessor": {
    "ProcessingIntervalMs": 5000,
    "RetryIntervalMs": 30000,
    "BatchSize": 100,
    "RetryTimeoutMinutes": 30,
    "MaxRetries": 3
  }
}
```

### Consumer Service Settings

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379", 
    "Kafka": "localhost:9092"
  },
  "ConsumerGroups": [
    {
      "GroupName": "default-consumer-group",
      "Topics": ["user-events", "order-events"]
    },
    {
      "GroupName": "analytics-group",
      "Topics": ["analytics-events"]
    }
  ]
}
```

## API Endpoints

### Producer Service (http://localhost:5299)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/messages/send` | Send a new message |
| POST | `/api/messages/acknowledge` | Acknowledge message processing |
| GET | `/api/messages/status/{id}` | Get message status |
| GET | `/api/messages/pending` | Get pending messages |
| GET | `/api/messages/consumer-group/{group}` | Get messages for consumer group |
| GET | `/api/messages/unacknowledged/{group}` | Get unacknowledged messages |
| DELETE | `/api/messages/{id}` | Delete message |
| GET | `/api/messages/health` | Health check |

### Consumer Service (http://localhost:5287)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/consumer/processed/{group}` | Get processed message IDs |
| POST | `/api/consumer/test-process` | Test message processing |
| GET | `/api/consumer/health` | Health check |

## Message Flow

1. **Send Message**: Client sends message to Producer API
2. **Store in Outbox**: Producer stores message in Redis outbox
3. **Background Processing**: Outbox processor sends message to Kafka
4. **Consumer Processing**: Consumer receives and processes message
5. **Acknowledgment**: Consumer sends acknowledgment to Producer
6. **Cleanup**: Producer updates message status and cleans up

## Monitoring

### Kafka UI (http://localhost:8080)
- View topics and partitions
- Monitor consumer groups
- See message flow

### Redis Commander (http://localhost:8081)
- View outbox data structure
- Monitor Redis keys and values
- Debug message states

### Logs
Both services provide detailed logging:
- Message processing status
- Error handling
- Retry attempts
- Performance metrics

## Troubleshooting

### Common Issues

1. **Redis Connection Failed**
   ```
   Solution: Ensure Redis is running via docker-compose
   ```

2. **Kafka Connection Failed** 
   ```
   Solution: Ensure Kafka is running and accessible on port 9092
   ```

3. **Messages Not Being Processed**
   ```
   Check: Consumer service logs and Kafka UI for consumer group status
   ```

4. **Duplicate Message Processing**
   ```
   Check: Redis tracking keys and consumer idempotency logic
   ```

## Development

### Running Tests
```powershell
dotnet test
```

### Building Docker Images
```powershell
# Producer Service
docker build -f ProducerService/Dockerfile -t outbox-producer .

# Consumer Service  
docker build -f ConsumerService/Dockerfile -t outbox-consumer .
```

## Production Considerations

- Configure appropriate Redis persistence
- Set up Kafka cluster with replication
- Implement proper monitoring and alerting
- Configure security (authentication/authorization)
- Set up load balancing for multiple service instances
- Configure appropriate retry policies and timeouts
