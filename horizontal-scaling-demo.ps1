# Horizontal Scaling Demo Script for Producer and Consumer Services
# This script demonstrates running multiple producer and consumer instances

Write-Host "Outbox Pattern - Horizontal Scaling Demo" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Set up environment variables for different producer instances
$producerInstances = @(
  @{ ServiceId = "producer-001"; Port = 5299 },
  @{ ServiceId = "producer-002"; Port = 5300 },
  @{ ServiceId = "producer-003"; Port = 5301 }
)

$consumerInstances = @(
  @{ ServiceId = "consumer-001"; Port = 5156 },
  @{ ServiceId = "consumer-002"; Port = 5157 },
  @{ ServiceId = "consumer-003"; Port = 5158 }
)

Write-Host "Starting Docker Compose services..." -ForegroundColor Yellow
docker-compose up -d postgres kafka zookeeper

# Wait for services to be ready
Start-Sleep -Seconds 10

Write-Host "Starting Producer Service Instances..." -ForegroundColor Yellow
$producerJobs = @()

foreach ($instance in $producerInstances) {
  $env:SERVICE_ID = $instance.ServiceId
  $env:PRODUCER_SERVICE_ID = $instance.ServiceId
  $env:INSTANCE_ID = "$($instance.ServiceId)-$(Get-Random)"
  $env:ASPNETCORE_URLS = "http://localhost:$($instance.Port)"
    
  Write-Host "  Starting Producer $($instance.ServiceId) on port $($instance.Port)..." -ForegroundColor Cyan
    
  $job = Start-Job -ScriptBlock {
    param($serviceId, $port, $workingDir)
        
    $env:SERVICE_ID = $serviceId
    $env:PRODUCER_SERVICE_ID = $serviceId
    $env:INSTANCE_ID = "$serviceId-$(Get-Random)"
    $env:ASPNETCORE_URLS = "http://localhost:$port"
        
    Set-Location $workingDir
    dotnet run --project ProducerService
  } -ArgumentList $instance.ServiceId, $instance.Port, (Get-Location)
    
  $producerJobs += $job
  Start-Sleep -Seconds 3
}

Write-Host "Starting Consumer Service Instances..." -ForegroundColor Yellow
$consumerJobs = @()

foreach ($instance in $consumerInstances) {
  $env:SERVICE_ID = $instance.ServiceId
  $env:CONSUMER_SERVICE_ID = $instance.ServiceId
  $env:INSTANCE_ID = "$($instance.ServiceId)-$(Get-Random)"
  $env:ASPNETCORE_URLS = "http://localhost:$($instance.Port)"
    
  Write-Host "  Starting Consumer $($instance.ServiceId) on port $($instance.Port)..." -ForegroundColor Cyan
    
  $job = Start-Job -ScriptBlock {
    param($serviceId, $port, $workingDir)
        
    $env:SERVICE_ID = $serviceId
    $env:CONSUMER_SERVICE_ID = $serviceId
    $env:INSTANCE_ID = "$serviceId-$(Get-Random)"
    $env:ASPNETCORE_URLS = "http://localhost:$port"
        
    Set-Location $workingDir
    dotnet run --project ConsumerService
  } -ArgumentList $instance.ServiceId, $instance.Port, (Get-Location)
    
  $consumerJobs += $job
  Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "All services started! Monitoring status..." -ForegroundColor Green
Write-Host ""
Write-Host "Producer Services:" -ForegroundColor Yellow
foreach ($instance in $producerInstances) {
  Write-Host "  - $($instance.ServiceId): http://localhost:$($instance.Port)" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Consumer Services:" -ForegroundColor Yellow
foreach ($instance in $consumerInstances) {
  Write-Host "  - $($instance.ServiceId): http://localhost:$($instance.Port)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Demonstration Commands:" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host ""
Write-Host "1. View all registered agents:" -ForegroundColor Yellow
Write-Host "   curl http://localhost:5299/api/agents/producers" -ForegroundColor White
Write-Host "   curl http://localhost:5299/api/agents/consumers" -ForegroundColor White
Write-Host ""
Write-Host "2. Send messages through different producers:" -ForegroundColor Yellow
Write-Host "   curl -X POST http://localhost:5299/api/messages/send -H 'Content-Type: application/json' -d '{\"topic\":\"order-events\",\"consumerGroup\":\"default-consumer-group\",\"message\":\" { \\\"orderId\\\":\\\"ORDER-001\\\", \\\"amount\\\":100.00 }\"}'" -ForegroundColor White
Write-Host "   curl -X POST http://localhost:5300/api/messages/send -H 'Content-Type: application/json' -d '{\"topic\":\"user-events\",\"consumerGroup\":\"default-consumer-group\",\"message\":\" { \\\"userId\\\":\\\"USER-001\\\", \\\"action\\\":\\\"created\\\" }\"}'" -ForegroundColor White
Write-Host ""
Write-Host "3. Monitor outbox messages:" -ForegroundColor Yellow
Write-Host "   curl http://localhost:5299/api/messages/pending" -ForegroundColor White
Write-Host ""
Write-Host "4. Check agent health:" -ForegroundColor Yellow
Write-Host "   curl http://localhost:5299/api/agents/producers/producer-001" -ForegroundColor White
Write-Host ""

Write-Host "Press 'Q' to stop all services or any other key to continue monitoring..." -ForegroundColor Red
do {
  $key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    
  if ($key.Character -eq 'q' -or $key.Character -eq 'Q') {
    Write-Host ""
    Write-Host "Stopping all services..." -ForegroundColor Yellow
        
    # Stop all jobs
    $producerJobs | Stop-Job
    $consumerJobs | Stop-Job
        
    # Remove jobs
    $producerJobs | Remove-Job -Force
    $consumerJobs | Remove-Job -Force
        
    # Stop Docker services
    docker-compose down
        
    Write-Host "All services stopped." -ForegroundColor Green
    break
  }
    
  # Show job status
  Write-Host ""
  Write-Host "Service Status:" -ForegroundColor Green
  Write-Host "Producers: $($producerJobs | Where-Object { $_.State -eq 'Running' } | Measure-Object | Select-Object -ExpandProperty Count) running" -ForegroundColor Cyan
  Write-Host "Consumers: $($consumerJobs | Where-Object { $_.State -eq 'Running' } | Measure-Object | Select-Object -ExpandProperty Count) running" -ForegroundColor Cyan
    
  Start-Sleep -Seconds 5
} while ($true)
