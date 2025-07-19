# Quick Load Test - Small Scale Test for Verification
# Tests 1000 messages in batches of 50 to verify the system works

Write-Host "🧪 QUICK LOAD TEST - SYSTEM VERIFICATION" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

# Small scale configuration for testing
$TotalMessages = 1000      # 1000 messages for quick test
$BatchSize = 50           # 50 messages per batch
$ProducerPorts = @(5301, 5302, 5303)
$Topic = "shared-events"

Write-Host "📊 Test Configuration:" -ForegroundColor Cyan
Write-Host "  Total Messages: $TotalMessages" -ForegroundColor White
Write-Host "  Batch Size: $BatchSize" -ForegroundColor White
Write-Host "  Producer Ports: $($ProducerPorts -join ', ')" -ForegroundColor White

# Check producer health
Write-Host "`n🔍 Checking Producer Services..." -ForegroundColor Cyan
$healthyProducers = @()
foreach ($port in $ProducerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 10
        Write-Host "  ✅ Producer $port is healthy" -ForegroundColor Green
        $healthyProducers += $port
    } catch {
        Write-Host "  ❌ Producer $port is not responding" -ForegroundColor Red
    }
}

if ($healthyProducers.Count -eq 0) {
    Write-Host "❌ No healthy producers found!" -ForegroundColor Red
    exit 1
}

# Initialize counters
$messagesSent = 0
$messagesSuccess = 0
$messagesFailed = 0
$startTime = Get-Date

Write-Host "`n🚀 Starting Quick Load Test..." -ForegroundColor Green

# Calculate batches
$totalBatches = [math]::Ceiling($TotalMessages / $BatchSize)

for ($batchNum = 0; $batchNum -lt $totalBatches; $batchNum++) {
    $producerPort = $healthyProducers[$batchNum % $healthyProducers.Count]
    
    # Calculate actual batch size (last batch might be smaller)
    $currentBatchSize = if ($batchNum -eq $totalBatches - 1) {
        $TotalMessages - ($batchNum * $BatchSize)
    } else {
        $BatchSize
    }
    
    Write-Host "📤 Batch $($batchNum + 1)/$totalBatches to Producer $producerPort ($currentBatchSize messages)..." -ForegroundColor Cyan
    
    # Send batch
    $batchSuccess = 0
    $batchFailed = 0
    
    for ($i = 1; $i -le $currentBatchSize; $i++) {
        $messageId = "test-$batchNum-$i-$(Get-Random)"
        $message = @{
            topic = $Topic
            message = "Quick test message $messageId from Producer $producerPort at $(Get-Date -Format 'HH:mm:ss.fff')"
        } | ConvertTo-Json
        
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$producerPort/api/messages/send" -Method POST -Body $message -ContentType "application/json" -TimeoutSec 10
            $messagesSent++
            
            if ($response.Status -eq "Queued") {
                $messagesSuccess++
                $batchSuccess++
            } else {
                $messagesFailed++
                $batchFailed++
            }
        } catch {
            $messagesSent++
            $messagesFailed++
            $batchFailed++
            Write-Host "    ❌ Failed to send message $i`: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "  ✅ Batch complete: $batchSuccess success, $batchFailed failed" -ForegroundColor Green
    
    # Small delay between batches
    Start-Sleep -Milliseconds 100
}

# Final results
$endTime = Get-Date
$duration = $endTime - $startTime
$averageRate = if ($duration.TotalSeconds -gt 0) { $messagesSuccess / $duration.TotalSeconds } else { 0 }
$successRate = if ($messagesSent -gt 0) { ($messagesSuccess / $messagesSent) * 100 } else { 0 }

Write-Host "`n🎉 QUICK TEST COMPLETED!" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "📊 Results:" -ForegroundColor Cyan
Write-Host "  Duration: $($duration.ToString('mm\:ss\.fff'))" -ForegroundColor White
Write-Host "  Messages Sent: $messagesSent" -ForegroundColor White
Write-Host "  Messages Success: $messagesSuccess" -ForegroundColor White
Write-Host "  Messages Failed: $messagesFailed" -ForegroundColor White
Write-Host "  Success Rate: $([math]::Round($successRate, 2))%" -ForegroundColor White
Write-Host "  Average Rate: $([math]::Round($averageRate, 1)) messages/second" -ForegroundColor White

# Test consumer response
Write-Host "`n🔍 Testing Consumer Response..." -ForegroundColor Cyan
$consumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$healthyConsumers = 0

foreach ($port in $consumerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 5
        if ($response.Status -eq "Healthy") {
            $healthyConsumers++
        }
    } catch {
        # Consumer might not be responding
    }
}

Write-Host "  ✅ Healthy Consumers: $healthyConsumers/6" -ForegroundColor Green

# Wait a moment for message processing
Write-Host "`n⏳ Waiting 10 seconds for message processing..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Check Kafka topics
Write-Host "`n📡 Checking Kafka Topic..." -ForegroundColor Cyan
try {
    $topicInfo = docker exec outbox-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic shared-events
    Write-Host "  ✅ Topic 'shared-events' exists" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Could not verify topic" -ForegroundColor Red
}

Write-Host "`n🎯 System Status:" -ForegroundColor Cyan
if ($successRate -gt 90) {
    Write-Host "  ✅ EXCELLENT! System is working properly" -ForegroundColor Green
    Write-Host "  Ready for full load test with 10 million messages" -ForegroundColor Green
} elseif ($successRate -gt 75) {
    Write-Host "  ✅ GOOD! System is mostly working" -ForegroundColor Green
    Write-Host "  Consider checking for any error messages" -ForegroundColor Yellow
} else {
    Write-Host "  ⚠️  Issues detected! Check system logs" -ForegroundColor Yellow
    Write-Host "  Run: docker-compose logs -f" -ForegroundColor White
}

Write-Host "`n🚀 Next Steps:" -ForegroundColor Cyan
Write-Host "  • Run full load test: .\load-test-producers.ps1" -ForegroundColor White
Write-Host "  • Monitor consumers: .\monitor-consumers.ps1" -ForegroundColor White
Write-Host "  • Check Kafka UI: http://localhost:8080" -ForegroundColor White
Write-Host "  • Check database: http://localhost:8082" -ForegroundColor White

Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
