# Load Testing Demo Script
# This script demonstrates different load testing scenarios

Write-Host "🧪 Load Testing Demo for Docker Scaled Outbox System" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green

Write-Host "`n📋 Available Load Testing Scripts:" -ForegroundColor Cyan
Write-Host "1. load-test-simple.ps1 - Simple load testing with configurable parameters" -ForegroundColor White
Write-Host "2. load-test-10m.ps1 - High-volume load testing (10 million messages)" -ForegroundColor White

Write-Host "`n🎯 Testing Scenarios:" -ForegroundColor Cyan

Write-Host "`n📊 1. Quick Test (100 messages)" -ForegroundColor Yellow
Write-Host "   Command: .\load-test-simple.ps1 -TotalMessages 100 -BatchSize 10 -MaxConcurrency 5 -Verbose" -ForegroundColor Gray

Write-Host "`n📊 2. Medium Test (10,000 messages)" -ForegroundColor Yellow
Write-Host "   Command: .\load-test-simple.ps1 -TotalMessages 10000 -BatchSize 100 -MaxConcurrency 20" -ForegroundColor Gray

Write-Host "`n📊 3. Large Test (100,000 messages)" -ForegroundColor Yellow
Write-Host "   Command: .\load-test-simple.ps1 -TotalMessages 100000 -BatchSize 500 -MaxConcurrency 50" -ForegroundColor Gray

Write-Host "`n📊 4. Extreme Test (10 million messages)" -ForegroundColor Yellow
Write-Host "   Command: .\load-test-10m.ps1 -TotalMessages 10000000 -BatchSize 500 -MaxConcurrentBatches 50" -ForegroundColor Gray

Write-Host "`n🚀 Running Quick Test (100 messages) as demonstration..." -ForegroundColor Green

# Run a quick test
try {
    & "$PSScriptRoot\load-test-simple.ps1" -TotalMessages 100 -BatchSize 10 -MaxConcurrency 5 -Verbose
} catch {
    Write-Host "`n❌ Error running test: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure the Docker system is running with: .\docker-manager.ps1 -Action Start" -ForegroundColor Yellow
}

Write-Host "`n📈 Performance Monitoring:" -ForegroundColor Cyan
Write-Host "• Kafka UI: http://localhost:8080" -ForegroundColor White
Write-Host "• pgAdmin: http://localhost:8082" -ForegroundColor White
Write-Host "• Check Docker logs: docker-compose logs -f" -ForegroundColor White

Write-Host "`n⚡ Load Testing Tips:" -ForegroundColor Cyan
Write-Host "• Start with small batches (10-100 messages) to test system stability" -ForegroundColor White
Write-Host "• Monitor system resources (CPU, Memory, Disk) during testing" -ForegroundColor White
Write-Host "• Check producer and consumer logs for errors" -ForegroundColor White
Write-Host "• Use Kafka UI to monitor message flow and consumer lag" -ForegroundColor White
Write-Host "• Scale up gradually to find the system's limits" -ForegroundColor White

Write-Host "`n🎛️ Parameters:" -ForegroundColor Cyan
Write-Host "• TotalMessages: Number of messages to send" -ForegroundColor White
Write-Host "• BatchSize: Messages per batch" -ForegroundColor White
Write-Host "• MaxConcurrency: Maximum concurrent requests" -ForegroundColor White
Write-Host "• Topic: Kafka topic name (default: shared-events)" -ForegroundColor White
Write-Host "• Verbose: Show detailed progress" -ForegroundColor White
