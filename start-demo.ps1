# Outbox Pattern Setup Script
# This script starts the infrastructure and services for the Outbox Pattern demo

Write-Host "🚀 Starting Outbox Pattern Demo Environment" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

# Function to check if Docker is running
function Test-DockerRunning {
  try {
    docker version | Out-Null
    return $true
  }
  catch {
    return $false
  }
}

# Function to wait for service to be ready
function Wait-ForService {
  param($ServiceName, $Url, $MaxAttempts = 30)
    
  Write-Host "⏳ Waiting for $ServiceName to be ready..." -ForegroundColor Yellow
    
  for ($i = 1; $i -le $MaxAttempts; $i++) {
    try {
      $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 5 -ErrorAction Stop
      if ($response.StatusCode -eq 200) {
        Write-Host "✅ $ServiceName is ready!" -ForegroundColor Green
        return $true
      }
    }
    catch {
      Write-Host "   Attempt $i/$MaxAttempts - Still waiting..." -ForegroundColor Gray
      Start-Sleep -Seconds 2
    }
  }
    
  Write-Host "❌ $ServiceName failed to start after $MaxAttempts attempts" -ForegroundColor Red
  return $false
}

# Check Docker
if (-not (Test-DockerRunning)) {
  Write-Host "❌ Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
  exit 1
}

Write-Host "✅ Docker is running" -ForegroundColor Green

# Start infrastructure
Write-Host "`n📦 Starting PostgreSQL and Kafka infrastructure..." -ForegroundColor Cyan
try {
  docker-compose up -d
  Write-Host "✅ Infrastructure containers started" -ForegroundColor Green
}
catch {
  Write-Host "❌ Failed to start infrastructure" -ForegroundColor Red
  exit 1
}

# Wait for PostgreSQL
Write-Host "`n🔄 Checking PostgreSQL connection..." -ForegroundColor Cyan
for ($i = 1; $i -le 30; $i++) {
  try {
    $pgTest = docker exec outbox-postgres pg_isready -U outbox_user -d outbox_db 2>$null
    if ($pgTest -like "*accepting connections*") {
      Write-Host "✅ PostgreSQL is ready!" -ForegroundColor Green
      break
    }
  }
  catch {
    Write-Host "   Attempt $i/30 - Waiting for PostgreSQL..." -ForegroundColor Gray
    Start-Sleep -Seconds 2
  }
}

# Wait for Kafka
Write-Host "`n🔄 Checking Kafka connection..." -ForegroundColor Cyan
Start-Sleep -Seconds 10  # Give Kafka more time to start

# Create topics
Write-Host "`n📝 Creating Kafka topics..." -ForegroundColor Cyan
$topics = @("user-events", "order-events", "analytics-events", "notification-events")

foreach ($topic in $topics) {
  try {
    docker exec outbox-kafka kafka-topics --create --topic $topic --bootstrap-server localhost:9092 --partitions 3 --replication-factor 1 --if-not-exists 2>$null
    Write-Host "✅ Topic '$topic' created" -ForegroundColor Green
  }
  catch {
    Write-Host "⚠️  Topic '$topic' may already exist or Kafka not ready" -ForegroundColor Yellow
  }
}

# Build and start Producer Service
Write-Host "`n🏗️  Building and starting Producer Service..." -ForegroundColor Cyan
Start-Process -FilePath "powershell" -ArgumentList "-Command", "cd ProducerService; dotnet build; dotnet run" -WindowStyle Normal
Start-Sleep -Seconds 5

# Build and start Consumer Service  
Write-Host "`n🏗️  Building and starting Consumer Service..." -ForegroundColor Cyan
Start-Process -FilePath "powershell" -ArgumentList "-Command", "cd ConsumerService; dotnet build; dotnet run" -WindowStyle Normal
Start-Sleep -Seconds 5

# Wait for services to be ready
Wait-ForService "Producer Service" "http://localhost:5299/api/messages/health"
Wait-ForService "Consumer Service" "http://localhost:5287/api/consumer/health"

Write-Host "`n🎉 Outbox Pattern Demo Environment is Ready!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

Write-Host "`n📊 Service URLs:" -ForegroundColor Cyan
Write-Host "  • Producer Service:    http://localhost:5299" -ForegroundColor White
Write-Host "  • Consumer Service:    http://localhost:5287" -ForegroundColor White
Write-Host "  • Producer API Docs:   http://localhost:5299/swagger" -ForegroundColor White
Write-Host "  • Consumer API Docs:   http://localhost:5287/swagger" -ForegroundColor White

Write-Host "`n🖥️  Monitoring UIs:" -ForegroundColor Cyan
Write-Host "  • Kafka UI:           http://localhost:8080 (if enabled)" -ForegroundColor White

Write-Host "`n🧪 Test Commands:" -ForegroundColor Cyan
Write-Host "  # Register a topic first:" -ForegroundColor Gray
Write-Host '  curl -X POST "http://localhost:5299/api/topics/register" -H "Content-Type: application/json" -d "{\"topicName\":\"test-events\",\"description\":\"Test events\",\"consumerGroups\":[{\"consumerGroupName\":\"default-consumer-group\",\"requiresAcknowledgment\":true}]}"' -ForegroundColor White

Write-Host "`n  # Send a test message:" -ForegroundColor Gray
Write-Host '  curl -X POST "http://localhost:5299/api/messages/send" -H "Content-Type: application/json" -d "{\"topic\":\"test-events\",\"message\":\"Test message\"}"' -ForegroundColor White

Write-Host "`n  # Check pending messages:" -ForegroundColor Gray
Write-Host '  curl "http://localhost:5299/api/messages/pending"' -ForegroundColor White

Write-Host "`n  # Check processed messages:" -ForegroundColor Gray
Write-Host '  curl "http://localhost:5287/api/consumer/processed/default-consumer-group"' -ForegroundColor White

Write-Host "`n📚 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Use the .http files in VS Code to test the APIs" -ForegroundColor White
Write-Host "  2. Monitor message flow in Kafka UI" -ForegroundColor White
Write-Host "  3. Check outbox data in PostgreSQL database" -ForegroundColor White
Write-Host "  4. Review logs in the service console windows" -ForegroundColor White

Write-Host "`n⚠️  To stop everything:" -ForegroundColor Yellow
Write-Host "  docker-compose down" -ForegroundColor White
Write-Host "  (Then close the service console windows)" -ForegroundColor White

Write-Host "`nPress any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
