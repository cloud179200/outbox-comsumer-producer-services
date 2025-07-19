# Simple Working Load Test Script
Write-Host "=== SIMPLE LOAD TEST ===" -ForegroundColor Green
Write-Host "Testing system with basic load..." -ForegroundColor Cyan

# Configuration
$TotalMessages = 50
$BatchSize = 5
$ProducerPorts = @(5301, 5302, 5303)
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$Topic = "user-events"

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Total Messages: $TotalMessages"
Write-Host "  Batch Size: $BatchSize"
Write-Host "  Producer Ports: $($ProducerPorts -join ', ')"
Write-Host "  Consumer Ports: $($ConsumerPorts -join ', ')"

# Check producer health
Write-Host "`nChecking Producer Health..." -ForegroundColor Cyan
$healthyProducers = @()
foreach ($port in $ProducerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 10
        Write-Host "  Producer $port: $($response.Status)" -ForegroundColor Green
        $healthyProducers += $port
    } catch {
        Write-Host "  Producer $port: Error - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Check consumer health
Write-Host "`nChecking Consumer Health..." -ForegroundColor Cyan
$healthyConsumers = @()
foreach ($port in $ConsumerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 10
        Write-Host "  Consumer $port: $($response.Status)" -ForegroundColor Green
        $healthyConsumers += $port
    } catch {
        Write-Host "  Consumer $port: Error - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nSystem Status:" -ForegroundColor Yellow
Write-Host "  Healthy Producers: $($healthyProducers.Count)/$($ProducerPorts.Count)"
Write-Host "  Healthy Consumers: $($healthyConsumers.Count)/$($ConsumerPorts.Count)"

if ($healthyProducers.Count -eq 0) {
    Write-Host "No healthy producers found! Exiting..." -ForegroundColor Red
    exit 1
}

# Initialize counters
$messagesSent = 0
$messagesSuccess = 0
$messagesFailed = 0
$startTime = Get-Date

Write-Host "`nStarting Load Test..." -ForegroundColor Green

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
    
    Write-Host "  Batch $($batchNum + 1)/$totalBatches to Producer $producerPort - $currentBatchSize messages..." -ForegroundColor Cyan
    
    # Send batch
    $batchSuccess = 0
    $batchFailed = 0
    
    for ($i = 1; $i -le $currentBatchSize; $i++) {
        $messageId = "test-$batchNum-$i-$(Get-Random)"
        $message = @{
            topic = $Topic
            message = "Load test message $messageId from Producer $producerPort at $(Get-Date -Format 'HH:mm:ss.fff')"
        } | ConvertTo-Json
        
        try {
            $messagesSent++
            $response = Invoke-RestMethod -Uri "http://localhost:$producerPort/api/messages/send" -Method POST -Body $message -ContentType "application/json" -TimeoutSec 10
            
            if ($response.Status -eq "Queued") {
                $messagesSuccess++
                $batchSuccess++
            } else {
                $messagesFailed++
                $batchFailed++
            }
        } catch {
            $messagesFailed++
            $batchFailed++
            Write-Host "    Error sending message $i`: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "    Batch complete: $batchSuccess success, $batchFailed failed" -ForegroundColor Green
    
    # Small delay between batches
    Start-Sleep -Milliseconds 100
}

# Final results
$endTime = Get-Date
$duration = $endTime - $startTime
$averageRate = if ($duration.TotalSeconds -gt 0) { $messagesSuccess / $duration.TotalSeconds } else { 0 }
$successRate = if ($messagesSent -gt 0) { ($messagesSuccess / $messagesSent) * 100 } else { 0 }

Write-Host "`n=== LOAD TEST COMPLETED ===" -ForegroundColor Green
Write-Host "Results:" -ForegroundColor Yellow
Write-Host "  Duration: $($duration.ToString('mm\:ss\.fff'))"
Write-Host "  Messages Sent: $messagesSent"
Write-Host "  Messages Success: $messagesSuccess"
Write-Host "  Messages Failed: $messagesFailed"
Write-Host "  Success Rate: $([math]::Round($successRate, 2))%"
Write-Host "  Average Rate: $([math]::Round($averageRate, 1)) messages/second"

# Test consumer response after load
Write-Host "`nTesting Consumer Response After Load..." -ForegroundColor Cyan
$healthyConsumersAfter = 0

foreach ($port in $ConsumerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 10
        Write-Host "  Consumer $port: $($response.Status)" -ForegroundColor Green
        $healthyConsumersAfter++
    } catch {
        Write-Host "  Consumer $port: Error - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nFinal System Status:" -ForegroundColor Yellow
Write-Host "  Healthy Consumers After Load: $healthyConsumersAfter/$($ConsumerPorts.Count)"

if ($successRate -gt 90) {
    Write-Host "`nSYSTEM STATUS: EXCELLENT" -ForegroundColor Green
    Write-Host "Ready for full load testing with millions of messages" -ForegroundColor Green
} elseif ($successRate -gt 75) {
    Write-Host "`nSYSTEM STATUS: GOOD" -ForegroundColor Yellow
    Write-Host "System is mostly working, minor issues detected" -ForegroundColor Yellow
} else {
    Write-Host "`nSYSTEM STATUS: ISSUES DETECTED" -ForegroundColor Red
    Write-Host "Check system logs for errors" -ForegroundColor Red
}

Write-Host "`nLoad Test Complete!" -ForegroundColor Green
