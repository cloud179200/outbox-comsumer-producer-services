# Comprehensive Load Test and Monitoring Script
# Tests message flow from producers to consumers with real-time monitoring

Write-Host "🚀 COMPREHENSIVE LOAD TEST - PRODUCERS TO CONSUMERS" -ForegroundColor Green
Write-Host "===================================================" -ForegroundColor Green

# Configuration
$TotalMessages = 10000000  # 10 million messages
$BatchSize = 500          # Messages per batch
$ProducerPorts = @(5301, 5302, 5303)
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$Topic = "shared-events"
$MaxConcurrentJobs = 15
$TestDurationMinutes = 30  # Maximum test duration

Write-Host "📊 Test Configuration:" -ForegroundColor Cyan
Write-Host "  Total Messages: $($TotalMessages.ToString('N0'))" -ForegroundColor White
Write-Host "  Batch Size: $BatchSize" -ForegroundColor White
Write-Host "  Producer Ports: $($ProducerPorts -join ', ')" -ForegroundColor White
Write-Host "  Consumer Ports: $($ConsumerPorts -join ', ')" -ForegroundColor White
Write-Host "  Max Test Duration: $TestDurationMinutes minutes" -ForegroundColor White

# Check system health
Write-Host "`n🔍 System Health Check..." -ForegroundColor Cyan

$healthyProducers = @()
foreach ($port in $ProducerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 5
        Write-Host "  ✅ Producer $port: $($response.Status)" -ForegroundColor Green
        $healthyProducers += $port
    } catch {
        Write-Host "  ❌ Producer $port: Not responding" -ForegroundColor Red
    }
}

$healthyConsumers = @()
foreach ($port in $ConsumerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 5
        Write-Host "  ✅ Consumer $port: $($response.Status)" -ForegroundColor Green
        $healthyConsumers += $port
    } catch {
        Write-Host "  ❌ Consumer $port: Not responding" -ForegroundColor Red
    }
}

if ($healthyProducers.Count -eq 0 -or $healthyConsumers.Count -eq 0) {
    Write-Host "❌ System not ready! Please start the Docker system first." -ForegroundColor Red
    Write-Host "Run: .\docker-manager.ps1 -Action Start" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ System Ready: $($healthyProducers.Count) producers, $($healthyConsumers.Count) consumers" -ForegroundColor Green

# Warning and confirmation
Write-Host "`n⚠️  HIGH LOAD WARNING!" -ForegroundColor Yellow
Write-Host "This will send $($TotalMessages.ToString('N0')) messages and heavily load your system!" -ForegroundColor Yellow
Write-Host "Make sure you have adequate system resources (CPU, Memory, Disk)" -ForegroundColor Yellow
$confirm = Read-Host "Continue with load test? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Load test cancelled." -ForegroundColor Yellow
    exit 0
}

# Initialize tracking
$script:StartTime = Get-Date
$script:EndTime = $script:StartTime.AddMinutes($TestDurationMinutes)
$script:MessagesSent = 0
$script:MessagesSuccess = 0
$script:MessagesFailed = 0
$script:BatchesCompleted = 0
$script:BatchesFailed = 0
$script:LockObject = New-Object System.Object

# Progress tracking function
function Update-Stats {
    param(
        [int]$SentCount = 0,
        [int]$SuccessCount = 0,
        [int]$FailedCount = 0,
        [int]$CompletedBatches = 0,
        [int]$FailedBatches = 0
    )
    
    [System.Threading.Monitor]::Enter($script:LockObject)
    try {
        $script:MessagesSent += $SentCount
        $script:MessagesSuccess += $SuccessCount
        $script:MessagesFailed += $FailedCount
        $script:BatchesCompleted += $CompletedBatches
        $script:BatchesFailed += $FailedBatches
    }
    finally {
        [System.Threading.Monitor]::Exit($script:LockObject)
    }
}

# Message sending function
$SendMessageBatch = {
    param(
        [int]$ProducerPort,
        [int]$BatchNumber,
        [int]$BatchSize,
        [string]$Topic
    )
    
    $batchResults = @{
        Sent = 0
        Success = 0
        Failed = 0
        Errors = @()
    }
    
    try {
        for ($i = 1; $i -le $BatchSize; $i++) {
            $messageId = "msg-$BatchNumber-$i-$(Get-Random)"
            $message = @{
                topic = $Topic
                message = "Load test message $messageId from Producer $ProducerPort at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')"
            } | ConvertTo-Json
            
            try {
                $response = Invoke-RestMethod -Uri "http://localhost:$ProducerPort/api/messages/send" -Method POST -Body $message -ContentType "application/json" -TimeoutSec 10
                $batchResults.Sent++
                if ($response.Status -eq "Queued") {
                    $batchResults.Success++
                } else {
                    $batchResults.Failed++
                }
            } catch {
                $batchResults.Failed++
                $batchResults.Errors += $_.Exception.Message
            }
        }
        
        return $batchResults
    } catch {
        $batchResults.Failed = $BatchSize
        $batchResults.Errors += $_.Exception.Message
        return $batchResults
    }
}

# Start monitoring job
$MonitoringJob = Start-Job -ScriptBlock {
    param($ConsumerPorts, $StartTime, $TestDurationMinutes)
    
    $monitoringInterval = 10  # seconds
    $reportInterval = 30      # seconds
    $lastReportTime = $StartTime
    
    while ((Get-Date) -lt $StartTime.AddMinutes($TestDurationMinutes)) {
        $currentTime = Get-Date
        $totalElapsed = $currentTime - $StartTime
        
        # Collect consumer stats
        $consumerStats = @()
        $totalProcessed = 0
        $healthyCount = 0
        
        foreach ($port in $ConsumerPorts) {
            try {
                $health = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 3
                if ($health.Status -eq "Healthy") {
                    $healthyCount++
                }
                $consumerStats += @{
                    Port = $port
                    Status = $health.Status
                    Timestamp = $currentTime
                }
            } catch {
                $consumerStats += @{
                    Port = $port
                    Status = "Error"
                    Timestamp = $currentTime
                }
            }
        }
        
        # Report every 30 seconds
        if (($currentTime - $lastReportTime).TotalSeconds -ge $reportInterval) {
            Write-Host "📊 Consumer Status - $($currentTime.ToString('HH:mm:ss')) | Healthy: $healthyCount/$($ConsumerPorts.Count) | Elapsed: $($totalElapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Blue
            $lastReportTime = $currentTime
        }
        
        Start-Sleep -Seconds $monitoringInterval
    }
} -ArgumentList $ConsumerPorts, $script:StartTime, $TestDurationMinutes

# Main load test execution
Write-Host "`n🔥 Starting Load Test..." -ForegroundColor Green
Write-Host "Test will run for maximum $TestDurationMinutes minutes or until all messages are sent" -ForegroundColor Yellow

$jobs = @()
$batchNumber = 0
$totalBatches = [math]::Ceiling($TotalMessages / $BatchSize)

try {
    while ($batchNumber -lt $totalBatches -and (Get-Date) -lt $script:EndTime) {
        # Limit concurrent jobs
        while ((Get-Job -State Running | Where-Object { $_.Name -ne "MonitoringJob" }).Count -ge $MaxConcurrentJobs) {
            Start-Sleep -Milliseconds 100
            
            # Process completed jobs
            $completedJobs = Get-Job -State Completed | Where-Object { $_.Name -ne "MonitoringJob" }
            foreach ($job in $completedJobs) {
                $result = Receive-Job -Job $job
                Remove-Job -Job $job
                
                if ($result.Success -ne $false) {
                    Update-Stats -SentCount $result.Sent -SuccessCount $result.Success -FailedCount $result.Failed -CompletedBatches 1
                } else {
                    Update-Stats -FailedCount $BatchSize -FailedBatches 1
                }
            }
        }
        
        # Select producer (round-robin)
        $producerPort = $healthyProducers[$batchNumber % $healthyProducers.Count]
        
        # Calculate batch size (last batch might be smaller)
        $currentBatchSize = if ($batchNumber -eq $totalBatches - 1) {
            $TotalMessages - ($batchNumber * $BatchSize)
        } else {
            $BatchSize
        }
        
        # Start batch job
        $job = Start-Job -ScriptBlock $SendMessageBatch -ArgumentList $producerPort, $batchNumber, $currentBatchSize, $Topic
        $jobs += $job
        $batchNumber++
        
        # Progress report every 100 batches
        if ($batchNumber % 100 -eq 0) {
            $elapsed = (Get-Date) - $script:StartTime
            $rate = if ($elapsed.TotalSeconds -gt 0) { $script:MessagesSuccess / $elapsed.TotalSeconds } else { 0 }
            $progressPercent = [math]::Round(($batchNumber / $totalBatches) * 100, 2)
            
            Write-Host "📈 Progress: $batchNumber/$totalBatches batches ($progressPercent%) | Success: $($script:MessagesSuccess.ToString('N0')) | Rate: $([math]::Round($rate, 0)) msg/s" -ForegroundColor Cyan
        }
    }
    
    # Wait for remaining jobs
    Write-Host "`n⏳ Waiting for remaining batches to complete..." -ForegroundColor Yellow
    
    while ((Get-Job -State Running | Where-Object { $_.Name -ne "MonitoringJob" }).Count -gt 0) {
        Start-Sleep -Seconds 1
        
        $completedJobs = Get-Job -State Completed | Where-Object { $_.Name -ne "MonitoringJob" }
        foreach ($job in $completedJobs) {
            $result = Receive-Job -Job $job
            Remove-Job -Job $job
            
            if ($result.Success -ne $false) {
                Update-Stats -SentCount $result.Sent -SuccessCount $result.Success -FailedCount $result.Failed -CompletedBatches 1
            } else {
                Update-Stats -FailedCount $BatchSize -FailedBatches 1
            }
        }
    }
    
} finally {
    # Clean up
    Get-Job | Where-Object { $_.Name -ne "MonitoringJob" } | Stop-Job
    Get-Job | Where-Object { $_.Name -ne "MonitoringJob" } | Remove-Job -Force
    
    # Stop monitoring
    if ($MonitoringJob) {
        Stop-Job -Job $MonitoringJob
        Remove-Job -Job $MonitoringJob -Force
    }
}

# Final results
$endTime = Get-Date
$totalDuration = $endTime - $script:StartTime
$averageRate = if ($totalDuration.TotalSeconds -gt 0) { $script:MessagesSuccess / $totalDuration.TotalSeconds } else { 0 }
$successRate = if ($script:MessagesSent -gt 0) { ($script:MessagesSuccess / $script:MessagesSent) * 100 } else { 0 }

Write-Host "`n🎉 LOAD TEST COMPLETED!" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "📊 Final Results:" -ForegroundColor Cyan
Write-Host "  Duration: $($totalDuration.ToString('hh\:mm\:ss'))" -ForegroundColor White
Write-Host "  Messages Sent: $($script:MessagesSent.ToString('N0'))" -ForegroundColor White
Write-Host "  Messages Success: $($script:MessagesSuccess.ToString('N0'))" -ForegroundColor White
Write-Host "  Messages Failed: $($script:MessagesFailed.ToString('N0'))" -ForegroundColor White
Write-Host "  Batches Completed: $($script:BatchesCompleted.ToString('N0'))" -ForegroundColor White
Write-Host "  Batches Failed: $($script:BatchesFailed.ToString('N0'))" -ForegroundColor White
Write-Host "  Success Rate: $([math]::Round($successRate, 2))%" -ForegroundColor White
Write-Host "  Average Rate: $([math]::Round($averageRate, 0)) messages/second" -ForegroundColor White

# Performance assessment
Write-Host "`n🎯 Performance Assessment:" -ForegroundColor Cyan
if ($averageRate -gt 2000) {
    Write-Host "  🚀 EXCELLENT! System handled very high load efficiently" -ForegroundColor Green
} elseif ($averageRate -gt 1000) {
    Write-Host "  ✅ VERY GOOD! System performed well under high load" -ForegroundColor Green
} elseif ($averageRate -gt 500) {
    Write-Host "  ✅ GOOD! System handled the load adequately" -ForegroundColor Green
} elseif ($averageRate -gt 100) {
    Write-Host "  ⚠️  MODERATE! Consider optimizing for higher throughput" -ForegroundColor Yellow
} else {
    Write-Host "  ❌ LOW! System may need optimization or more resources" -ForegroundColor Red
}

Write-Host "`n🔍 Next Steps:" -ForegroundColor Cyan
Write-Host "  • Check consumer processing with: .\monitor-consumers.ps1" -ForegroundColor White
Write-Host "  • Monitor Kafka UI: http://localhost:8080" -ForegroundColor White
Write-Host "  • Check database: http://localhost:8082 (pgAdmin)" -ForegroundColor White
Write-Host "  • View service logs: docker-compose logs -f" -ForegroundColor White

Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
