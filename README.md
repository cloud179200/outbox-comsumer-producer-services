# Outbox Pattern with PostgreSQL and Kafka (Docker-Based)

This project implements the Outbox Pattern using PostgreSQL for reliable message delivery between producer and consumer services with Kafka as the message broker. The entire system is containerized using Docker for easy deployment and scaling.

## Architecture

The solution consists of two ASP.NET Core services running in Docker containers:

### Producer Service
- **API for Message Publishing**: Receives messages and topic information
- **Outbox Storage**: Stores messages in PostgreSQL with tracking information
- **Background Processing**: Continuously processes pending messages and sends them to Kafka
- **Acknowledgment Handling**: Tracks consumer acknowledgments and handles retries
- **Agent Management**: Manages service instances and heartbeat monitoring

### Consumer Service
- **Kafka Consumer**: Consumes messages from multiple topics with different consumer groups
- **Message Processing**: Processes messages with idempotency guarantees
- **Outbox Tracking**: Tracks processed messages to prevent duplicate processing
- **Acknowledgment**: Sends acknowledgments back to producer service
- **Heartbeat Monitoring**: Tracks consumer instance health

## Key Features

✅ **Reliable Message Delivery**: Outbox pattern ensures no message loss  
✅ **Idempotency**: Prevents duplicate message processing  
✅ **Retry Logic**: Automatic retry for failed messages  
✅ **Multiple Consumer Groups**: Support for different consumer groups and topics  
✅ **Horizontal Scaling**: Docker-based scaling of producer and consumer instances  
✅ **Health Monitoring**: Instance heartbeat tracking and health checks  
✅ **Database Persistence**: PostgreSQL for reliable data storage  
✅ **Monitoring**: Kafka UI for system monitoring  
✅ **Containerized**: Full Docker deployment with docker-compose

## Prerequisites

- Docker Desktop
- PowerShell (for management scripts)

## Quick Start

### 1. Start the Complete System

Start the entire system with scaled instances:

```powershell
.\docker-manager.ps1 -Action Start
```

This will start:
- PostgreSQL (port 5432)
- Kafka (port 9092)
- Zookeeper (port 2181)
- Kafka UI (http://localhost:8080)
- 3 Producer Service instances (ports 5299, 5399, 5499)
- 6 Consumer Service instances (ports 5287, 5387, 5487, 5587, 5687, 5787)

### 2. Simple Start (Infrastructure Only)

For development or testing with local services:

```powershell
.\docker-simple.ps1 -Action Start
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
.\docker-manager.ps1 -Action Stop
```

## Docker Management Scripts

### docker-manager.ps1
Primary management script for the scaled Docker system:
- Start/Stop complete system with multiple instances
- Health monitoring and logging
- Scaling configuration

### docker-simple.ps1  
Simple infrastructure management:
- Start/Stop infrastructure only (PostgreSQL, Kafka, Zookeeper, Kafka-UI)
- Useful for development with local services

### docker-test.ps1
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
docker-compose up -d --scale outbox-producer=5 --scale outbox-consumer=10
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
docker-compose logs -f outbox-producer
docker-compose logs -f outbox-consumer
```

## Development

### Building Docker Images

Images are built automatically by docker-compose, but you can build manually:

```powershell
# Build both services
docker-compose build

# Build specific service
docker-compose build outbox-producer
docker-compose build outbox-consumer
```

### Local Development

For local development, you can run infrastructure only and develop services locally:

```powershell
# Start infrastructure
.\docker-simple.ps1 -Action Start

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
