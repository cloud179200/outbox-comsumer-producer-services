# Enhanced E2E Load Test - 10M Batch + 5K No-Batch in Parallel
param(
    [string]$TestType = "full", # Options: "full", "batch-only", "nobatch-only", "quick"
    [int]$BatchMessages = 10000000,
    [int]$NoBatchMessages = 5000,
    [bool]$Verbose = $true
)

$ErrorActionPreference = "Stop"
$SessionId = [System.Guid]::NewGuid().ToString("N").Substring(0, 8)
$TestStartTime = Get-Date

Write-Host "================================================================" -ForegroundColor Green
Write-Host "ENHANCED E2E LOAD TEST - PARALLEL PROCESSING" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
Write-Host "Test Type: $TestType" -ForegroundColor Yellow

# Adjust for quick test
if ($TestType -eq "quick") {
    $BatchMessages = 100
    $NoBatchMessages = 50
    Write-Host "QUICK TEST MODE: Batch=$BatchMessages, No-Batch=$NoBatchMessages" -ForegroundColor Cyan
}

Write-Host "Batch Messages: $BatchMessages" -ForegroundColor Yellow
Write-Host "No-Batch Messages: $NoBatchMessages" -ForegroundColor Yellow
Write-Host "Test Start Time: $($TestStartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
Write-Host ""

# Configuration
$ProducerUrls = @("http://localhost:5301", "http://localhost:5302", "http://localhost:5303")
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$ConsumerGroups = @("group-a", "group-b", "group-c")
$Topic = "shared-events"

# Function to check service health
function Test-ServiceHealth {
    param([string]$Url, [string]$ServiceType = "service")
    
    try {
        $healthUrl = if ($ServiceType -eq "producer") { "$Url/api/messages/health" } else { "$Url/api/consumer/health" }
        $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 5
        return $response.Status -eq "Healthy"
    }
    catch {
        return $false
    }
}

# Function to send messages in batch to a producer
function Send-BatchMessages {
    param(
        [string]$ProducerUrl,
        [int]$MessageCount,
        [string]$SessionId,
        [string]$TestType
    )
    
    $result = @{
        ProducerUrl = $ProducerUrl
        MessageCount = $MessageCount
        Success = 0
        Failed = 0
        Errors = @()
        Duration = 0
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    # For large batches, send in chunks
    $chunkSize = 1000
    $chunks = [Math]::Ceiling($MessageCount / $chunkSize)
    
    for ($chunk = 0; $chunk -lt $chunks; $chunk++) {
        $startIndex = $chunk * $chunkSize
        $endIndex = [Math]::Min($startIndex + $chunkSize - 1, $MessageCount - 1)
        $currentChunkSize = $endIndex - $startIndex + 1
        
        try {
            $messages = @()
            for ($i = $startIndex; $i -le $endIndex; $i++) {
                $message = "SESSION:$SessionId | $TestType Load Test Message $($i + 1) - Producer: $ProducerUrl - Chunk: $($chunk + 1)/$chunks"
                $messages += $message
            }
            
            # Try batch endpoint first, fall back to individual sends
            try {
                $body = @{
                    Topic = $Topic
                    Messages = $messages
                    UseBatching = $true
                } | ConvertTo-Json -Depth 3
                
                $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send-batch" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 120
                
                if ($response -and $response.Success) {
                    $result.Success += $currentChunkSize
                    if ($Verbose -and ($chunk % 10 -eq 0)) {
                        Write-Host "  Batch chunk $($chunk + 1)/$chunks sent to $ProducerUrl" -ForegroundColor Green
                    }
                }
                else {
                    throw "Batch send failed: $($response.Error)"
                }
            }
            catch {
                # Fall back to individual message sending
                if ($Verbose) {
                    Write-Host "  Batch send failed, using individual sends for chunk $($chunk + 1)" -ForegroundColor Yellow
                }
                
                foreach ($message in $messages) {
                    try {
                        $singleBody = @{
                            Topic = $Topic
                            Message = $message
                            UseBatching = $true
                        } | ConvertTo-Json
                        
                        $singleResponse = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send" -Method Post -Body $singleBody -ContentType "application/json" -TimeoutSec 30
                        
                        if ($singleResponse) {
                            $result.Success++
                        }
                        else {
                            $result.Failed++
                        }
                    }
                    catch {
                        $result.Failed++
                        $result.Errors += "Single message failed: $($_.Exception.Message)"
                    }
                }
            }
        }
        catch {
            $result.Failed += $currentChunkSize
            $result.Errors += "Chunk $($chunk + 1) failed: $($_.Exception.Message)"
            if ($Verbose) {
                Write-Host "  Chunk $($chunk + 1) failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    
    $stopwatch.Stop()
    $result.Duration = $stopwatch.ElapsedMilliseconds
    
    return $result
}

# Step 1: Check all producer services
Write-Host "Step 1: Verifying all producer services health..." -ForegroundColor Cyan
$healthyProducers = @()

foreach ($url in $ProducerUrls) {
    if (Test-ServiceHealth -Url $url -ServiceType "producer") {
        $healthyProducers += $url
        Write-Host "  ✅ Producer $url is healthy" -ForegroundColor Green
    }
    else {
        Write-Host "  ❌ Producer $url is not responding" -ForegroundColor Red
    }
}

if ($healthyProducers.Count -eq 0) {
    throw "No healthy producers found! Please start the Docker system first."
}

Write-Host "Found $($healthyProducers.Count) healthy producers out of $($ProducerUrls.Count)" -ForegroundColor Green
Write-Host ""

# Step 2: Check consumer services
Write-Host "Step 2: Discovering consumer services..." -ForegroundColor Cyan
$healthyConsumers = @()

foreach ($port in $ConsumerPorts) {
    $consumerUrl = "http://localhost:$port"
    if (Test-ServiceHealth -Url $consumerUrl -ServiceType "consumer") {
        $healthyConsumers += $consumerUrl
        Write-Host "  ✅ Consumer $consumerUrl is healthy" -ForegroundColor Green
    }
    else {
        Write-Host "  ❌ Consumer $consumerUrl is not responding" -ForegroundColor Yellow
    }
}

Write-Host "Found $($healthyConsumers.Count) healthy consumers out of $($ConsumerPorts.Count)" -ForegroundColor Green
Write-Host ""

# Step 3: Execute load tests in parallel
$testResults = @{}

if ($TestType -eq "full" -or $TestType -eq "batch-only") {
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "EXECUTING HIGH-VOLUME BATCH LOAD TEST" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "Messages: $BatchMessages (UseBatching = true)" -ForegroundColor Yellow
    Write-Host "Producers: $($healthyProducers.Count)" -ForegroundColor Yellow
    Write-Host "Distribution: $($BatchMessages / $healthyProducers.Count) messages per producer" -ForegroundColor Yellow
    Write-Host ""
    
    # Distribute batch messages across producers
    $messagesPerProducer = [Math]::Ceiling($BatchMessages / $healthyProducers.Count)
    $batchJobs = @()
    
    for ($i = 0; $i -lt $healthyProducers.Count; $i++) {
        $producer = $healthyProducers[$i]
        $startIndex = $i * $messagesPerProducer
        $remainingMessages = $BatchMessages - $startIndex
        $currentMessageCount = [Math]::Min($messagesPerProducer, $remainingMessages)
        
        if ($currentMessageCount -gt 0) {
            Write-Host "Starting batch job for $producer - $currentMessageCount messages" -ForegroundColor Yellow
            
            $job = Start-Job -ScriptBlock {
                param($ProducerUrl, $MessageCount, $SessionId, $Topic, $Verbose)
                
                # Recreate the function inside the job
                function Send-BatchMessages {
                    param([string]$ProducerUrl, [int]$MessageCount, [string]$SessionId, [string]$TestType)
                    
                    $result = @{
                        ProducerUrl = $ProducerUrl
                        MessageCount = $MessageCount
                        Success = 0
                        Failed = 0
                        Errors = @()
                        Duration = 0
                    }
                    
                    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
                    $chunkSize = 1000
                    $chunks = [Math]::Ceiling($MessageCount / $chunkSize)
                    
                    for ($chunk = 0; $chunk -lt $chunks; $chunk++) {
                        $startIndex = $chunk * $chunkSize
                        $endIndex = [Math]::Min($startIndex + $chunkSize - 1, $MessageCount - 1)
                        $currentChunkSize = $endIndex - $startIndex + 1
                        
                        try {
                            $messages = @()
                            for ($i = $startIndex; $i -le $endIndex; $i++) {
                                $message = "SESSION:$SessionId | Batch Load Test Message $($i + 1) - Producer: $ProducerUrl - Chunk: $($chunk + 1)/$chunks"
                                $messages += $message
                            }
                            
                            # Send individual messages (more reliable)
                            foreach ($message in $messages) {
                                try {
                                    $singleBody = @{
                                        Topic = $Topic
                                        Message = $message
                                        UseBatching = $true
                                    } | ConvertTo-Json
                                    
                                    $singleResponse = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send" -Method Post -Body $singleBody -ContentType "application/json" -TimeoutSec 30
                                    
                                    if ($singleResponse) {
                                        $result.Success++
                                    }
                                    else {
                                        $result.Failed++
                                    }
                                }
                                catch {
                                    $result.Failed++
                                    $result.Errors += $_.Exception.Message
                                }
                            }
                        }
                        catch {
                            $result.Failed += $currentChunkSize
                            $result.Errors += "Chunk $($chunk + 1) failed: $($_.Exception.Message)"
                        }
                    }
                    
                    $stopwatch.Stop()
                    $result.Duration = $stopwatch.ElapsedMilliseconds
                    return $result
                }
                
                return Send-BatchMessages -ProducerUrl $ProducerUrl -MessageCount $MessageCount -SessionId $SessionId -TestType "Batch"
            } -ArgumentList $producer, $currentMessageCount, $SessionId, $Topic, $Verbose
            
            $batchJobs += @{
                Job = $job
                Producer = $producer
                MessageCount = $currentMessageCount
            }
        }
    }
    
    Write-Host "Waiting for batch jobs to complete..." -ForegroundColor Yellow
    $batchJobs | ForEach-Object { $_.Job } | Wait-Job | Out-Null
    
    # Collect batch results
    $batchResults = @()
    $totalBatchSent = 0
    $totalBatchFailed = 0
    
    foreach ($jobInfo in $batchJobs) {
        $result = Receive-Job -Job $jobInfo.Job
        $batchResults += $result
        $totalBatchSent += $result.Success
        $totalBatchFailed += $result.Failed
        
        Write-Host "  $($result.ProducerUrl): $($result.Success) sent, $($result.Failed) failed" -ForegroundColor White
        Remove-Job -Job $jobInfo.Job
    }
    
    $batchSuccessRate = if (($totalBatchSent + $totalBatchFailed) -gt 0) { 
        [Math]::Round(($totalBatchSent / ($totalBatchSent + $totalBatchFailed)) * 100, 2) 
    } else { 0 }
    
    $testResults["Batch"] = @{
        MessagesSent = $totalBatchSent
        MessagesFailed = $totalBatchFailed
        SuccessRate = $batchSuccessRate
    }
    
    Write-Host ""
    Write-Host "Batch Test Results:" -ForegroundColor Green
    Write-Host "  Messages Sent: $totalBatchSent" -ForegroundColor Green
    Write-Host "  Messages Failed: $totalBatchFailed" -ForegroundColor Red
    Write-Host "  Success Rate: $batchSuccessRate%" -ForegroundColor White
    Write-Host ""
}

if ($TestType -eq "full" -or $TestType -eq "nobatch-only") {
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "EXECUTING IMMEDIATE PROCESSING LOAD TEST" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "Messages: $NoBatchMessages (UseBatching = false)" -ForegroundColor Yellow
    Write-Host "Producers: $($healthyProducers.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    # Distribute no-batch messages across producers
    $messagesPerProducer = [Math]::Ceiling($NoBatchMessages / $healthyProducers.Count)
    $noBatchJobs = @()
    
    for ($i = 0; $i -lt $healthyProducers.Count; $i++) {
        $producer = $healthyProducers[$i]
        $startIndex = $i * $messagesPerProducer
        $remainingMessages = $NoBatchMessages - $startIndex
        $currentMessageCount = [Math]::Min($messagesPerProducer, $remainingMessages)
        
        if ($currentMessageCount -gt 0) {
            Write-Host "Starting no-batch job for $producer - $currentMessageCount messages" -ForegroundColor Yellow
            
            $job = Start-Job -ScriptBlock {
                param($ProducerUrl, $MessageCount, $SessionId, $Topic)
                
                $result = @{
                    ProducerUrl = $ProducerUrl
                    MessageCount = $MessageCount
                    Success = 0
                    Failed = 0
                    Errors = @()
                }
                
                for ($i = 1; $i -le $MessageCount; $i++) {
                    try {
                        $message = "SESSION:$SessionId | No-Batch Load Test Message $i - Producer: $ProducerUrl"
                        
                        $body = @{
                            Topic = $Topic
                            Message = $message
                            UseBatching = $false
                        } | ConvertTo-Json
                        
                        $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
                        
                        if ($response) {
                            $result.Success++
                        }
                        else {
                            $result.Failed++
                        }
                    }
                    catch {
                        $result.Failed++
                        $result.Errors += $_.Exception.Message
                    }
                }
                
                return $result
            } -ArgumentList $producer, $currentMessageCount, $SessionId, $Topic
            
            $noBatchJobs += @{
                Job = $job
                Producer = $producer
                MessageCount = $currentMessageCount
            }
        }
    }
    
    Write-Host "Waiting for no-batch jobs to complete..." -ForegroundColor Yellow
    $noBatchJobs | ForEach-Object { $_.Job } | Wait-Job | Out-Null
    
    # Collect no-batch results
    $noBatchResults = @()
    $totalNoBatchSent = 0
    $totalNoBatchFailed = 0
    
    foreach ($jobInfo in $noBatchJobs) {
        $result = Receive-Job -Job $jobInfo.Job
        $noBatchResults += $result
        $totalNoBatchSent += $result.Success
        $totalNoBatchFailed += $result.Failed
        
        Write-Host "  $($result.ProducerUrl): $($result.Success) sent, $($result.Failed) failed" -ForegroundColor White
        Remove-Job -Job $jobInfo.Job
    }
    
    $noBatchSuccessRate = if (($totalNoBatchSent + $totalNoBatchFailed) -gt 0) { 
        [Math]::Round(($totalNoBatchSent / ($totalNoBatchSent + $totalNoBatchFailed)) * 100, 2) 
    } else { 0 }
    
    $testResults["NoBatch"] = @{
        MessagesSent = $totalNoBatchSent
        MessagesFailed = $totalNoBatchFailed
        SuccessRate = $noBatchSuccessRate
    }
    
    Write-Host ""
    Write-Host "No-Batch Test Results:" -ForegroundColor Green
    Write-Host "  Messages Sent: $totalNoBatchSent" -ForegroundColor Green
    Write-Host "  Messages Failed: $totalNoBatchFailed" -ForegroundColor Red
    Write-Host "  Success Rate: $noBatchSuccessRate%" -ForegroundColor White
}

# Final Summary
Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "COMPREHENSIVE LOAD TEST FINAL RESULTS" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
Write-Host "Test Type: $TestType" -ForegroundColor Yellow

$totalMessagesSent = 0
$totalMessagesFailed = 0

foreach ($testName in $testResults.Keys) {
    $result = $testResults[$testName]
    $totalMessagesSent += $result.MessagesSent
    $totalMessagesFailed += $result.MessagesFailed
    
    Write-Host ""
    Write-Host "$testName Test:" -ForegroundColor Cyan
    Write-Host "  Messages Sent: $($result.MessagesSent)" -ForegroundColor Green
    Write-Host "  Messages Failed: $($result.MessagesFailed)" -ForegroundColor Red
    Write-Host "  Success Rate: $($result.SuccessRate)%" -ForegroundColor White
}

$overallSuccessRate = if (($totalMessagesSent + $totalMessagesFailed) -gt 0) { 
    [Math]::Round(($totalMessagesSent / ($totalMessagesSent + $totalMessagesFailed)) * 100, 2) 
} else { 0 }

Write-Host ""
Write-Host "Overall Summary:" -ForegroundColor Yellow
Write-Host "  Total Messages Sent: $totalMessagesSent" -ForegroundColor Green
Write-Host "  Total Messages Failed: $totalMessagesFailed" -ForegroundColor Red
Write-Host "  Overall Success Rate: $overallSuccessRate%" -ForegroundColor White
Write-Host "  Producers Used: $($healthyProducers.Count)" -ForegroundColor White
Write-Host "  Consumers Available: $($healthyConsumers.Count)" -ForegroundColor White

if ($overallSuccessRate -ge 95) {
    Write-Host ""
    Write-Host "🎉 EXCELLENT: Load test passed with outstanding performance!" -ForegroundColor Green
} elseif ($overallSuccessRate -ge 80) {
    Write-Host ""
    Write-Host "✅ GOOD: Load test passed with good performance!" -ForegroundColor Green
} elseif ($overallSuccessRate -ge 50) {
    Write-Host ""
    Write-Host "⚠️ PARTIAL: Load test completed with some issues!" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "❌ FAILED: Load test failed - investigate issues!" -ForegroundColor Red
}

$testEndTime = Get-Date
$totalDuration = $testEndTime - $TestStartTime
Write-Host ""
Write-Host "Test Duration: $($totalDuration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
Write-Host "Test completed at $testEndTime" -ForegroundColor Gray

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  • Monitor message processing: .\verify-acknowledgments.ps1" -ForegroundColor White
Write-Host "  • Check consumer verification (after processing delay)" -ForegroundColor White
Write-Host "  • View Kafka UI: http://localhost:8080" -ForegroundColor White
