# Simple Load Test - Basic Version
Write-Host "Simple Load Test Starting..." -ForegroundColor Green

# Configuration
$TotalMessages = 100
$ProducerPorts = @(5301, 5302, 5303)
$Topic = "shared-events"

Write-Host "Total Messages: $TotalMessages"
Write-Host "Producer Ports: $($ProducerPorts -join ', ')"

# Check producer health
Write-Host "Checking Producer Services..."
$healthyProducers = @()
foreach ($port in $ProducerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 10
        Write-Host "Producer $port is healthy" -ForegroundColor Green
        $healthyProducers += $port
    } catch {
        Write-Host "Producer $port is not responding" -ForegroundColor Red
    }
}

if ($healthyProducers.Count -eq 0) {
    Write-Host "No healthy producers found!" -ForegroundColor Red
    exit 1
}

# Initialize counters
$messagesSent = 0
$messagesSuccess = 0
$messagesFailed = 0
$startTime = Get-Date

Write-Host "Starting Load Test..."

# Send messages
for ($i = 1; $i -le $TotalMessages; $i++) {
    $producerPort = $healthyProducers[($i - 1) % $healthyProducers.Count]
    
    $messageId = "test-$i-$(Get-Random)"
    $messageText = "Load test message $messageId from Producer $producerPort"
    
    $message = @{
        topic = $Topic
        message = $messageText
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$producerPort/api/messages/send" -Method POST -Body $message -ContentType "application/json" -TimeoutSec 10
        $messagesSent++
        
        if ($response.Status -eq "Queued") {
            $messagesSuccess++
        } else {
            $messagesFailed++
        }
    } catch {
        $messagesSent++
        $messagesFailed++
        Write-Host "Failed to send message $i" -ForegroundColor Red
    }
    
    # Progress every 10 messages
    if ($i % 10 -eq 0) {
        Write-Host "Sent $i messages..."
    }
}

# Final results
$endTime = Get-Date
$duration = $endTime - $startTime
$averageRate = if ($duration.TotalSeconds -gt 0) { $messagesSuccess / $duration.TotalSeconds } else { 0 }
$successRate = if ($messagesSent -gt 0) { ($messagesSuccess / $messagesSent) * 100 } else { 0 }

Write-Host "LOAD TEST COMPLETED!" -ForegroundColor Green
Write-Host "Results:"
Write-Host "  Messages Sent: $messagesSent"
Write-Host "  Messages Success: $messagesSuccess"
Write-Host "  Messages Failed: $messagesFailed"
Write-Host "  Success Rate: $([math]::Round($successRate, 2))%"
Write-Host "  Average Rate: $([math]::Round($averageRate, 1)) messages/second"

# Test consumer response
Write-Host "Testing Consumer Response..."
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

Write-Host "Healthy Consumers: $healthyConsumers/6" -ForegroundColor Green

if ($successRate -gt 90) {
    Write-Host "EXCELLENT! System is working properly" -ForegroundColor Green
    Write-Host "Ready for full load test with 10 million messages" -ForegroundColor Green
} elseif ($successRate -gt 75) {
    Write-Host "GOOD! System is mostly working" -ForegroundColor Green
} else {
    Write-Host "Issues detected! Check system logs" -ForegroundColor Yellow
}

Write-Host "Available Load Test Scripts:"
Write-Host "  load-test-producers.ps1  - 10,000,000 messages (full load)"
Write-Host "  monitor-consumers.ps1    - Monitor consumer performance"

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
