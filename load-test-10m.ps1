# High-Volume Load Testing Script for Docker Scaled Outbox System
# Sends 10 million messages concurrently to 3 producer services in batches of 500

param(
    [int]$TotalMessages = 10000000,  # 10 million messages
    [int]$BatchSize = 500,           # 500 messages per batch
    [int]$MaxConcurrentBatches = 50, # Maximum concurrent batches
    [int]$DelayBetweenBatches = 100, # Milliseconds delay between batches
    [string]$Topic = "shared-events",
    [switch]$SkipHealthCheck,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Producer endpoints
$ProducerEndpoints = @(
    "http://localhost:5301/api/messages/send",
    "http://localhost:5302/api/messages/send", 
    "http://localhost:5303/api/messages/send"
)

# Statistics tracking
$Statistics = @{
    TotalSent = 0
    TotalFailed = 0
    StartTime = $null
    EndTime = $null
    BatchesCompleted = 0
    TotalBatches = 0
    ProducerStats = @{
        Producer1 = @{ Sent = 0; Failed = 0 }
        Producer2 = @{ Sent = 0; Failed = 0 }
        Producer3 = @{ Sent = 0; Failed = 0 }
    }
}

# Thread-safe collections for statistics
$script:SentCount = 0
$script:FailedCount = 0
$script:BatchesCompletedCount = 0
$script:ProducerCounters = @{
    Producer1 = @{ Sent = 0; Failed = 0 }
    Producer2 = @{ Sent = 0; Failed = 0 }
    Producer3 = @{ Sent = 0; Failed = 0 }
}

function Write-StatusMessage {
    param([string]$Message, [string]$Color = "White")
    
    $timestamp = Get-Date -Format "HH:mm:ss.fff"
    Write-Host "[$timestamp] $Message" -ForegroundColor $Color
}

function Test-ProducerHealth {
    param([string]$ProducerUrl)
    
    try {
        $healthUrl = $ProducerUrl.Replace("/api/messages/send", "/api/messages/health")
        $response = Invoke-RestMethod -Uri $healthUrl -Method GET -TimeoutSec 5
        return $response.Status -eq "Healthy"
    } catch {
        return $false
    }
}

function Send-MessageBatch {
    param(
        [string]$ProducerUrl,
        [int]$ProducerIndex,
        [int]$BatchNumber,
        [int]$BatchSize,
        [string]$Topic
    )
    
    $producerName = "Producer$($ProducerIndex + 1)"
    $batchStartTime = Get-Date
    $successCount = 0
    $failureCount = 0
    
    try {
        # Create batch of messages
        $tasks = @()
        
        for ($i = 0; $i -lt $BatchSize; $i++) {
            $messageId = [Guid]::NewGuid().ToString()
            $messageContent = @{
                messageId = $messageId
                timestamp = (Get-Date).ToString("O")
                batchNumber = $BatchNumber
                messageIndex = $i
                producerIndex = $ProducerIndex
                content = "Load test message $($BatchNumber * $BatchSize + $i + 1) from $producerName"
            } | ConvertTo-Json -Compress
            
            $requestBody = @{
                topic = $Topic
                message = $messageContent
            } | ConvertTo-Json
            
            # Create async task for each message
            $task = [System.Threading.Tasks.Task]::Run({
                param($url, $body)
                try {
                    $response = Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType "application/json" -TimeoutSec 10
                    return @{ Success = $true; Response = $response }
                } catch {
                    return @{ Success = $false; Error = $_.Exception.Message }
                }
            }.GetNewClosure(), @($ProducerUrl, $requestBody))
            
            $tasks += $task
        }
        
        # Wait for all tasks in batch to complete
        $results = [System.Threading.Tasks.Task]::WhenAll($tasks).GetAwaiter().GetResult()
        
        # Count successes and failures
        foreach ($result in $results) {
            if ($result.Success) {
                $successCount++
            } else {
                $failureCount++
                if ($Verbose) {
                    Write-StatusMessage "Message failed: $($result.Error)" "Red"
                }
            }
        }
        
        # Update thread-safe counters
        [System.Threading.Interlocked]::Add([ref]$script:SentCount, $successCount) | Out-Null
        [System.Threading.Interlocked]::Add([ref]$script:FailedCount, $failureCount) | Out-Null
        [System.Threading.Interlocked]::Add([ref]$script:ProducerCounters[$producerName].Sent, $successCount) | Out-Null
        [System.Threading.Interlocked]::Add([ref]$script:ProducerCounters[$producerName].Failed, $failureCount) | Out-Null
        [System.Threading.Interlocked]::Increment([ref]$script:BatchesCompletedCount) | Out-Null
        
        $batchDuration = (Get-Date) - $batchStartTime
        $throughput = [math]::Round($successCount / $batchDuration.TotalSeconds, 2)
        
        if ($Verbose) {
            Write-StatusMessage "Batch $BatchNumber ($producerName): $successCount sent, $failureCount failed, $throughput msg/sec" "Green"
        }
        
        return @{
            Success = $true
            BatchNumber = $BatchNumber
            ProducerName = $producerName
            Sent = $successCount
            Failed = $failureCount
            Duration = $batchDuration.TotalMilliseconds
            Throughput = $throughput
        }
        
    } catch {
        [System.Threading.Interlocked]::Add([ref]$script:FailedCount, $BatchSize) | Out-Null
        [System.Threading.Interlocked]::Add([ref]$script:ProducerCounters[$producerName].Failed, $BatchSize) | Out-Null
        [System.Threading.Interlocked]::Increment([ref]$script:BatchesCompletedCount) | Out-Null
        
        Write-StatusMessage "Batch $BatchNumber ($producerName) failed completely: $($_.Exception.Message)" "Red"
        return @{
            Success = $false
            BatchNumber = $BatchNumber
            ProducerName = $producerName
            Error = $_.Exception.Message
        }
    }
}

function Show-RealTimeStats {
    param([int]$TotalBatches, [datetime]$StartTime)
    
    while ($script:BatchesCompletedCount -lt $TotalBatches) {
        $elapsed = (Get-Date) - $StartTime
        $progress = [math]::Round(($script:BatchesCompletedCount / $TotalBatches) * 100, 2)
        $avgThroughput = if ($elapsed.TotalSeconds -gt 0) { [math]::Round($script:SentCount / $elapsed.TotalSeconds, 2) } else { 0 }
        
        $eta = if ($script:BatchesCompletedCount -gt 0) {
            $remainingBatches = $TotalBatches - $script:BatchesCompletedCount
            $avgBatchTime = $elapsed.TotalSeconds / $script:BatchesCompletedCount
            [TimeSpan]::FromSeconds($remainingBatches * $avgBatchTime)
        } else {
            [TimeSpan]::Zero
        }
        
        Write-Host "`r                                                                                                                                " -NoNewline
        Write-Host "`r📊 Progress: $progress% | Sent: $($script:SentCount) | Failed: $($script:FailedCount) | Batches: $($script:BatchesCompletedCount)/$TotalBatches | Throughput: $avgThroughput msg/sec | ETA: $($eta.ToString('hh\:mm\:ss'))" -NoNewline -ForegroundColor Cyan
        
        Start-Sleep -Milliseconds 1000
    }
    Write-Host ""
}

# Main execution
Write-StatusMessage "🚀 Starting High-Volume Load Test for Docker Scaled Outbox System" "Green"
Write-StatusMessage "=================================================================" "Green"

# Configuration summary
Write-StatusMessage "📋 Configuration:" "Cyan"
Write-StatusMessage "  • Total Messages: $($TotalMessages:N0)" "White"
Write-StatusMessage "  • Batch Size: $BatchSize" "White"
Write-StatusMessage "  • Total Batches: $([math]::Ceiling($TotalMessages / $BatchSize))" "White"
Write-StatusMessage "  • Max Concurrent Batches: $MaxConcurrentBatches" "White"
Write-StatusMessage "  • Target Topic: $Topic" "White"
Write-StatusMessage "  • Producer Endpoints: $($ProducerEndpoints.Count)" "White"

# Health check
if (-not $SkipHealthCheck) {
    Write-StatusMessage "`n🩺 Performing health checks..." "Cyan"
    $healthyProducers = 0
    
    for ($i = 0; $i -lt $ProducerEndpoints.Count; $i++) {
        $isHealthy = Test-ProducerHealth $ProducerEndpoints[$i]
        if ($isHealthy) {
            $healthyProducers++
            Write-StatusMessage "  ✅ Producer $($i + 1) is healthy" "Green"
        } else {
            Write-StatusMessage "  ❌ Producer $($i + 1) is not healthy" "Red"
        }
    }
    
    if ($healthyProducers -eq 0) {
        Write-StatusMessage "❌ No healthy producers found. Exiting." "Red"
        exit 1
    }
    
    Write-StatusMessage "  ✅ $healthyProducers/$($ProducerEndpoints.Count) producers are healthy" "Green"
}

# Calculate batches
$TotalBatches = [math]::Ceiling($TotalMessages / $BatchSize)
$Statistics.TotalBatches = $TotalBatches
$Statistics.StartTime = Get-Date

Write-StatusMessage "`n🔄 Starting load test..." "Cyan"
Write-StatusMessage "  • Total batches to process: $TotalBatches" "White"
Write-StatusMessage "  • Estimated duration: $([math]::Round($TotalBatches * $DelayBetweenBatches / 1000 / 60, 2)) minutes (excluding processing time)" "White"

# Start real-time statistics display in background
$statsJob = Start-Job -ScriptBlock {
    param($TotalBatches, $StartTime, $script:BatchesCompletedCount, $script:SentCount, $script:FailedCount)
    
    while ($using:script:BatchesCompletedCount -lt $TotalBatches) {
        $elapsed = (Get-Date) - $StartTime
        $progress = [math]::Round(($using:script:BatchesCompletedCount / $TotalBatches) * 100, 2)
        $avgThroughput = if ($elapsed.TotalSeconds -gt 0) { [math]::Round($using:script:SentCount / $elapsed.TotalSeconds, 2) } else { 0 }
        
        Write-Host "`r📊 Progress: $progress% | Sent: $($using:script:SentCount) | Failed: $($using:script:FailedCount) | Batches: $($using:script:BatchesCompletedCount)/$TotalBatches | Throughput: $avgThroughput msg/sec" -NoNewline -ForegroundColor Cyan
        Start-Sleep -Seconds 2
    }
} -ArgumentList $TotalBatches, $Statistics.StartTime, $script:BatchesCompletedCount, $script:SentCount, $script:FailedCount

# Process batches with concurrency control
$runningJobs = @()

for ($batch = 0; $batch -lt $TotalBatches; $batch++) {
    # Wait if we've reached max concurrent batches
    while ($runningJobs.Count -ge $MaxConcurrentBatches) {
        $completedJobs = $runningJobs | Where-Object { $_.State -eq "Completed" }
        foreach ($job in $completedJobs) {
            $runningJobs = $runningJobs | Where-Object { $_.Id -ne $job.Id }
            Remove-Job $job
        }
        Start-Sleep -Milliseconds 50
    }
    
    # Select producer in round-robin fashion
    $producerIndex = $batch % $ProducerEndpoints.Count
    $producerUrl = $ProducerEndpoints[$producerIndex]
    
    # Calculate actual batch size (last batch might be smaller)
    $currentBatchSize = [math]::Min($BatchSize, $TotalMessages - ($batch * $BatchSize))
    
    # Start batch processing job
    $job = Start-Job -ScriptBlock {
        param($ProducerUrl, $ProducerIndex, $BatchNumber, $BatchSize, $Topic, $VerboseFlag)
        
        # Re-define the function in job context
        function Send-MessageBatch {
            param(
                [string]$ProducerUrl,
                [int]$ProducerIndex,
                [int]$BatchNumber,
                [int]$BatchSize,
                [string]$Topic
            )
            
            $producerName = "Producer$($ProducerIndex + 1)"
            $batchStartTime = Get-Date
            $successCount = 0
            $failureCount = 0
            
            try {
                for ($i = 0; $i -lt $BatchSize; $i++) {
                    $messageId = [Guid]::NewGuid().ToString()
                    $messageContent = @{
                        messageId = $messageId
                        timestamp = (Get-Date).ToString("O")
                        batchNumber = $BatchNumber
                        messageIndex = $i
                        producerIndex = $ProducerIndex
                        content = "Load test message $($BatchNumber * $BatchSize + $i + 1) from $producerName"
                    } | ConvertTo-Json -Compress
                    
                    $requestBody = @{
                        topic = $Topic
                        message = $messageContent
                    } | ConvertTo-Json
                    
                    try {
                        $null = Invoke-RestMethod -Uri $ProducerUrl -Method POST -Body $requestBody -ContentType "application/json" -TimeoutSec 10
                        $successCount++
                    } catch {
                        $failureCount++
                        if ($VerboseFlag) {
                            Write-Host "Message failed: $($_.Exception.Message)" -ForegroundColor Red
                        }
                    }
                }
                
                $batchDuration = (Get-Date) - $batchStartTime
                $throughput = if ($batchDuration.TotalSeconds -gt 0) { [math]::Round($successCount / $batchDuration.TotalSeconds, 2) } else { 0 }
                
                return @{
                    Success = $true
                    BatchNumber = $BatchNumber
                    ProducerName = $producerName
                    Sent = $successCount
                    Failed = $failureCount
                    Duration = $batchDuration.TotalMilliseconds
                    Throughput = $throughput
                }
                
            } catch {
                return @{
                    Success = $false
                    BatchNumber = $BatchNumber
                    ProducerName = $producerName
                    Error = $_.Exception.Message
                    Failed = $BatchSize
                    Sent = 0
                }
            }
        }
        
        return Send-MessageBatch -ProducerUrl $ProducerUrl -ProducerIndex $ProducerIndex -BatchNumber $BatchNumber -BatchSize $BatchSize -Topic $Topic
    } -ArgumentList $producerUrl, $producerIndex, $batch, $currentBatchSize, $Topic, $Verbose
    
    $runningJobs += $job
    
    # Small delay between batch starts
    if ($DelayBetweenBatches -gt 0) {
        Start-Sleep -Milliseconds $DelayBetweenBatches
    }
}

Write-StatusMessage "`n⏳ Waiting for all batches to complete..." "Yellow"

# Wait for all jobs to complete
while ($runningJobs.Count -gt 0) {
    $completedJobs = $runningJobs | Where-Object { $_.State -eq "Completed" }
    
    foreach ($job in $completedJobs) {
        $result = Receive-Job $job
        if ($result.Success) {
            $script:SentCount += $result.Sent
            $script:FailedCount += $result.Failed
            $script:ProducerCounters[$result.ProducerName].Sent += $result.Sent
            $script:ProducerCounters[$result.ProducerName].Failed += $result.Failed
            
            if ($Verbose) {
                Write-StatusMessage "Batch $($result.BatchNumber) ($($result.ProducerName)): $($result.Sent) sent, $($result.Failed) failed, $($result.Throughput) msg/sec" "Green"
            }
        } else {
            $script:FailedCount += $result.Failed
            $script:ProducerCounters[$result.ProducerName].Failed += $result.Failed
            Write-StatusMessage "Batch $($result.BatchNumber) ($($result.ProducerName)) failed: $($result.Error)" "Red"
        }
        
        $script:BatchesCompletedCount++
        Remove-Job $job
    }
    
    $runningJobs = $runningJobs | Where-Object { $_.State -ne "Completed" }
    
    # Show progress
    $progress = [math]::Round(($script:BatchesCompletedCount / $TotalBatches) * 100, 2)
    Write-Host "`r📊 Progress: $progress% | Completed: $($script:BatchesCompletedCount)/$TotalBatches batches | Sent: $($script:SentCount) | Failed: $($script:FailedCount)" -NoNewline -ForegroundColor Cyan
    
    Start-Sleep -Milliseconds 500
}

# Stop stats job
Stop-Job $statsJob -ErrorAction SilentlyContinue
Remove-Job $statsJob -ErrorAction SilentlyContinue

$Statistics.EndTime = Get-Date
$totalDuration = $Statistics.EndTime - $Statistics.StartTime

Write-Host "`n"
Write-StatusMessage "🎉 Load test completed!" "Green"
Write-StatusMessage "======================" "Green"

# Final statistics
Write-StatusMessage "`n📊 Final Statistics:" "Cyan"
Write-StatusMessage "  • Total Messages Attempted: $($TotalMessages.ToString('N0'))" "White"
Write-StatusMessage "  • Total Messages Sent: $($script:SentCount.ToString('N0'))" "Green"
Write-StatusMessage "  • Total Messages Failed: $($script:FailedCount.ToString('N0'))" "Red"
Write-StatusMessage "  • Success Rate: $([math]::Round(($script:SentCount / $TotalMessages) * 100, 2))%" "Green"
Write-StatusMessage "  • Total Duration: $($totalDuration.ToString('hh\:mm\:ss\.fff'))" "White"
Write-StatusMessage "  • Average Throughput: $([math]::Round($script:SentCount / $totalDuration.TotalSeconds, 2)) messages/second" "Cyan"
Write-StatusMessage "  • Total Batches: $TotalBatches" "White"

Write-StatusMessage "`n🏭 Producer Statistics:" "Cyan"
foreach ($producer in $script:ProducerCounters.Keys) {
    $stats = $script:ProducerCounters[$producer]
    $total = $stats.Sent + $stats.Failed
    $successRate = if ($total -gt 0) { [math]::Round(($stats.Sent / $total) * 100, 2) } else { 0 }
    Write-StatusMessage "  • $producer - Sent: $($stats.Sent.ToString('N0')), Failed: $($stats.Failed.ToString('N0')), Success Rate: $successRate%" "White"
}

Write-StatusMessage "`n⚡ Performance Metrics:" "Cyan"
Write-StatusMessage "  • Messages per batch: $BatchSize" "White"
Write-StatusMessage "  • Batches per second: $([math]::Round($TotalBatches / $totalDuration.TotalSeconds, 2))" "White"
Write-StatusMessage "  • Average batch duration: $([math]::Round($totalDuration.TotalMilliseconds / $TotalBatches, 2)) ms" "White"

if ($script:FailedCount -gt 0) {
    Write-StatusMessage "`n⚠️ Warning: $($script:FailedCount) messages failed. Check producer logs and system resources." "Yellow"
}

Write-StatusMessage "`n✅ Load test completed successfully!" "Green"
Write-StatusMessage "Check Kafka UI (http://localhost:8080) and producer logs for message processing details." "Gray"
