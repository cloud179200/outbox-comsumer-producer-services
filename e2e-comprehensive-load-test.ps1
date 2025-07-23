# Comprehensive E2E Load Test - Integrated Batch and No-Batch Testing
# Tests message delivery across all 3 producer services with parallel processing
# Includes both high-volume batching (10M messages) and immediate processing (5K messages)

param(
  [string]$TestType = "full", # Options: "full", "batch-only", "nobatch-only"
  [string]$Topic = "shared-events",
  [int]$BatchMessages = 10000000,
  [int]$NoBatchMessages = 5000,
  [int]$BatchSize = 1000,
  [int]$MaxConcurrentBatches = 100,
  [int]$MaxConcurrentMessages = 50,
  [int]$VerificationTimeoutSeconds = 300,
  [bool]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# Generate unique session ID for this test run
$SessionId = [System.Guid]::NewGuid().ToString("N").Substring(0, 8)
$TestStartTime = Get-Date

Write-Host "================================================================" -ForegroundColor Green
Write-Host "COMPREHENSIVE E2E LOAD TEST WITH PARALLEL PROCESSING" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
Write-Host "Test Type: $TestType" -ForegroundColor Yellow
Write-Host "Topic: $Topic" -ForegroundColor Yellow
Write-Host "Batch Messages: $BatchMessages" -ForegroundColor Yellow
Write-Host "No-Batch Messages: $NoBatchMessages" -ForegroundColor Yellow
Write-Host "Test Start Time: $($TestStartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
Write-Host ""

# Producer service configuration - All 3 services
$ProducerUrls = @(
    "http://localhost:5301",
    "http://localhost:5302", 
    "http://localhost:5303"
)

# Consumer service configuration
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$ConsumerGroups = @("group-a", "group-b", "group-c")

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

# Function to get registered consumer groups for a topic
function Get-RegisteredConsumerGroups {
    param([string]$ProducerUrl, [string]$TopicName)
    
    try {
        # First get the topic by name
        $topicUrl = "$ProducerUrl/api/topics/by-name/$TopicName"
        $topic = Invoke-RestMethod -Uri $topicUrl -Method Get -TimeoutSec 10
        
        if ($topic -and $topic.Id) {
            # Then get consumer groups for this topic
            $consumerGroupsUrl = "$ProducerUrl/api/topics/$($topic.Id)/consumer-groups"
            $consumerGroups = Invoke-RestMethod -Uri $consumerGroupsUrl -Method Get -TimeoutSec 10
            return $consumerGroups | Where-Object { $_.IsActive -eq $true }
        }
        
        return @()
    }
    catch {
        Write-Warning "Failed to get registered consumer groups: $_"
        return @()
    }
}

# Function to configure infinite retry for consumer groups
function Set-InfiniteRetry {
    param([string]$ProducerUrl, [array]$ConsumerGroups)
    
    Write-Host "Configuring infinite retry for all consumer groups..." -ForegroundColor Yellow
    
    foreach ($group in $ConsumerGroups) {
        $updateBody = @{
            ConsumerGroupName = $group.ConsumerGroupName
            RequiresAcknowledgment = $true
            AcknowledgmentTimeoutMinutes = 5
            MaxRetries = -1  # Set to -1 for infinite retry
        } | ConvertTo-Json
        
        try {
            $updateUrl = "$ProducerUrl/api/topics/consumer-groups/$($group.Id)"
            Invoke-RestMethod -Uri $updateUrl -Method Put -Body $updateBody -ContentType "application/json" -TimeoutSec 10
            Write-Host "  [OK] $($group.ConsumerGroupName): Infinite retry enabled" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to enable infinite retry for $($group.ConsumerGroupName): $_"
        }
    }
    Write-Host ""
}

# Function to send message batch (for high-volume test)
function Send-MessageBatch {
    param(
        [string]$ProducerUrl,
        [int]$BatchStartIndex,
        [int]$BatchSize,
        [string]$Topic,
        [string]$SessionId,
        [bool]$Verbose
    )
    
    $results = @{
        ProducerUrl = $ProducerUrl
        BatchStartIndex = $BatchStartIndex
        BatchSize = $BatchSize
        Sent = 0
        Failed = 0
        Errors = @()
        Duration = 0
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        $messages = @()
        for ($i = 0; $i -lt $BatchSize; $i++) {
            $messageIndex = $BatchStartIndex + $i
            $message = "SESSION:$SessionId | Batch Load Test Message $messageIndex - Batch: $BatchStartIndex-$($BatchStartIndex + $BatchSize - 1) - Producer: $ProducerUrl"
            $messages += $message
        }
        
        $body = @{
            Topic = $Topic
            Messages = $messages
            UseBatching = $true
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send-batch" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
        
        if ($response -and $response.Success) {
            $results.Sent = $BatchSize
            if ($Verbose) {
                Write-Host "[OK] Batch sent: $BatchSize messages to $ProducerUrl" -ForegroundColor Green
            }
        }
        else {
            $results.Failed = $BatchSize
            $results.Errors += "Batch send failed: $($response.Error)"
        }
    }
    catch {
        $results.Failed = $BatchSize
        $results.Errors += $_.Exception.Message
        if ($Verbose) {
            Write-Host "[ERROR] Batch failed for $ProducerUrl : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    finally {
        $stopwatch.Stop()
        $results.Duration = $stopwatch.ElapsedMilliseconds
    }
    
    return $results
}

# Function to send single message (for immediate processing test)
function Send-SingleMessage {
    param(
        [string]$ProducerUrl,
        [int]$MessageIndex,
        [string]$Topic,
        [string]$SessionId,
        [bool]$Verbose
    )
    
    $result = @{
        ProducerUrl = $ProducerUrl
        MessageIndex = $MessageIndex
        Success = $false
        Error = $null
        ResponseTime = 0
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        $message = "SESSION:$SessionId | No-Batch Load Test Message $MessageIndex - Producer: $ProducerUrl"
        
        $body = @{
            Topic = $Topic
            Message = $message
            UseBatching = $false
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
        
        if ($response) {
            $result.Success = $true
            if ($Verbose) {
                Write-Host "[OK] Message $MessageIndex sent to $ProducerUrl" -ForegroundColor Green
            }
        }
    }
    catch {
        $result.Error = $_.Exception.Message
        if ($Verbose) {
            Write-Host "[ERROR] Message $MessageIndex failed for $ProducerUrl : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    finally {
        $stopwatch.Stop()
        $result.ResponseTime = $stopwatch.ElapsedMilliseconds
    }
    
    return $result
}

# Function to get session-specific processed messages
function Get-SessionProcessedMessages {
    param([string]$ConsumerUrl, [string]$ConsumerGroup, [string]$SessionId, [DateTime]$TestStartTime)
    
    try {
        $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/processed/$ConsumerGroup" -Method Get -TimeoutSec 10
        
        if ($response) {
            $sessionMessages = $response | Where-Object { 
                $_.Content -like "*SESSION:$SessionId*" -and 
                [DateTime]::Parse($_.ProcessedAt) -ge $TestStartTime 
            }
            return $sessionMessages
        }
        return @()
    }
    catch {
        return @()
    }
}

# Function to get session-specific failed messages
function Get-SessionFailedMessages {
    param([string]$ConsumerUrl, [string]$ConsumerGroup, [string]$SessionId, [DateTime]$TestStartTime)
    
    try {
        $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/failed/$ConsumerGroup" -Method Get -TimeoutSec 10
        
        if ($response) {
            $sessionMessages = $response | Where-Object { 
                $_.Content -like "*SESSION:$SessionId*" -and 
                [DateTime]::Parse($_.FailedAt) -ge $TestStartTime 
            }
            return $sessionMessages
        }
        return @()
    }
    catch {
        return @()
    }
}

# Step 1: Verify all producer services health
Write-Host "Step 1: Verifying all producer services health..." -ForegroundColor Cyan
$healthyProducers = @()

foreach ($url in $ProducerUrls) {
    if (Test-ServiceHealth -Url $url -ServiceType "producer") {
        $healthyProducers += $url
        Write-Host "[OK] Producer service: $url" -ForegroundColor Green
    }
    else {
        Write-Host "[ERROR] Producer service not responding: $url" -ForegroundColor Red
    }
}

if ($healthyProducers.Count -eq 0) {
    throw "No healthy producer services found! Please start the Docker system first."
}

Write-Host "Found $($healthyProducers.Count) healthy producers out of $($ProducerUrls.Count)" -ForegroundColor Green
Write-Host ""

# Step 2: Discover consumer services
Write-Host "Step 2: Discovering consumer services..." -ForegroundColor Cyan
$healthyConsumers = @()
foreach ($port in $ConsumerPorts) {
    $consumerUrl = "http://localhost:$port"
    if (Test-ServiceHealth -Url $consumerUrl -ServiceType "consumer") {
        $healthyConsumers += $consumerUrl
        Write-Host "[OK] Consumer service: $consumerUrl" -ForegroundColor Green
    }
    else {
        Write-Host "[WARNING] Consumer service not responding: $consumerUrl" -ForegroundColor Yellow
    }
}

if ($healthyConsumers.Count -eq 0) {
    Write-Warning "No consumer services found, but continuing test..."
}
else {
    Write-Host "Found $($healthyConsumers.Count) healthy consumer services" -ForegroundColor Yellow
}
Write-Host ""

# Step 3: Get registered consumer groups
Write-Host "Step 3: Getting registered consumer groups..." -ForegroundColor Cyan
$consumerGroups = Get-RegisteredConsumerGroups -ProducerUrl $healthyProducers[0] -TopicName $Topic

if ($consumerGroups.Count -eq 0) {
    throw "No active consumer groups found for topic '$Topic'"
}

Write-Host "Found $($consumerGroups.Count) active consumer groups:" -ForegroundColor Green
foreach ($group in $consumerGroups) {
    Write-Host "  - $($group.ConsumerGroupName) (MaxRetries: $($group.MaxRetries))" -ForegroundColor White
}
Write-Host ""

# Step 4: Configure infinite retry
Set-InfiniteRetry -ProducerUrl $healthyProducers[0] -ConsumerGroups $consumerGroups

# Step 5: Execute Load Tests
$testResults = @{}

if ($TestType -eq "full" -or $TestType -eq "batch-only") {
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "EXECUTING HIGH-VOLUME BATCH LOAD TEST" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "Messages: $BatchMessages" -ForegroundColor Yellow
    Write-Host "Batch Size: $BatchSize" -ForegroundColor Yellow
    Write-Host "Producers: $($healthyProducers.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    # Calculate batch distribution across producers
    $totalBatches = [Math]::Ceiling($BatchMessages / $BatchSize)
    $batchesPerProducer = [Math]::Ceiling($totalBatches / $healthyProducers.Count)
    
    Write-Host "Batch Distribution:" -ForegroundColor Cyan
    Write-Host "  Total Batches: $totalBatches" -ForegroundColor White
    Write-Host "  Batches per Producer: $batchesPerProducer" -ForegroundColor White
    Write-Host ""
    
    # Prepare batch jobs
    $allBatchJobs = @()
    $currentMessageIndex = 1
    
    for ($producerIndex = 0; $producerIndex -lt $healthyProducers.Count; $producerIndex++) {
        $producerUrl = $healthyProducers[$producerIndex]
        
        for ($batchIndex = 0; $batchIndex -lt $batchesPerProducer -and $currentMessageIndex -le $BatchMessages; $batchIndex++) {
            $remainingMessages = $BatchMessages - $currentMessageIndex + 1
            $currentBatchSize = [Math]::Min($BatchSize, $remainingMessages)
            
            $batchJob = @{
                ProducerUrl = $producerUrl
                BatchStartIndex = $currentMessageIndex
                BatchSize = $currentBatchSize
                Topic = $Topic
                SessionId = $SessionId
                Verbose = $Verbose
            }
            
            $allBatchJobs += $batchJob
            $currentMessageIndex += $currentBatchSize
        }
    }
    
    Write-Host "Starting batch load test with $($allBatchJobs.Count) batches..." -ForegroundColor Cyan
    $batchStartTime = Get-Date
    
    # Execute batches in parallel
    $batchResults = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $MaxConcurrentBatches)
    $runspacePool.Open()
    
    $batchFunction = {
        param($ProducerUrl, $BatchStartIndex, $BatchSize, $Topic, $SessionId, $Verbose)
        
        $results = @{
            ProducerUrl = $ProducerUrl
            BatchStartIndex = $BatchStartIndex
            BatchSize = $BatchSize
            Sent = 0
            Failed = 0
            Errors = @()
            Duration = 0
        }
        
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        try {
            $messages = @()
            for ($i = 0; $i -lt $BatchSize; $i++) {
                $messageIndex = $BatchStartIndex + $i
                $message = "SESSION:$SessionId | Batch Load Test Message $messageIndex - Batch: $BatchStartIndex-$($BatchStartIndex + $BatchSize - 1) - Producer: $ProducerUrl"
                $messages += $message
            }
            
            $body = @{
                Topic = $Topic
                Messages = $messages
                UseBatching = $true
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send-batch" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
            
            if ($response -and $response.Success) {
                $results.Sent = $BatchSize
            }
            else {
                $results.Failed = $BatchSize
                $results.Errors += "Batch send failed: $($response.Error)"
            }
        }
        catch {
            $results.Failed = $BatchSize
            $results.Errors += $_.Exception.Message
        }
        finally {
            $stopwatch.Stop()
            $results.Duration = $stopwatch.ElapsedMilliseconds
        }
        
        return $results
    }
    
    # Create and start batch jobs
    $batchJobs = [System.Collections.ArrayList]@()
    foreach ($batchJob in $allBatchJobs) {
        $powerShell = [powershell]::Create()
        $powerShell.RunspacePool = $runspacePool
        
        [void]$powerShell.AddScript($batchFunction).AddParameters(@{
            ProducerUrl = $batchJob.ProducerUrl
            BatchStartIndex = $batchJob.BatchStartIndex
            BatchSize = $batchJob.BatchSize
            Topic = $batchJob.Topic
            SessionId = $batchJob.SessionId
            Verbose = $batchJob.Verbose
        })
        
        [void]$batchJobs.Add(@{
            PowerShell = $powerShell
            Handle = $powerShell.BeginInvoke()
        })
    }
    
    # Monitor batch completion
    $completedBatches = 0
    while ($batchJobs.Count -gt 0) {
        $completedIndices = @()
        
        for ($i = 0; $i -lt $batchJobs.Count; $i++) {
            if ($batchJobs[$i].Handle.IsCompleted) {
                try {
                    $result = $batchJobs[$i].PowerShell.EndInvoke($batchJobs[$i].Handle)
                    $batchResults.Add($result)
                    $completedBatches++
                    
                    if ($completedBatches % 100 -eq 0) {
                        $elapsed = (Get-Date) - $batchStartTime
                        Write-Host "Progress: $completedBatches/$($allBatchJobs.Count) batches completed - Elapsed: $($elapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Yellow
                    }
                    
                    $completedIndices += $i
                }
                catch {
                    Write-Warning "Error retrieving batch result: $($_.Exception.Message)"
                    $completedIndices += $i
                }
                finally {
                    $batchJobs[$i].PowerShell.Dispose()
                }
            }
        }
        
        for ($i = $completedIndices.Count - 1; $i -ge 0; $i--) {
            $batchJobs.RemoveAt($completedIndices[$i])
        }
        
        if ($batchJobs.Count -gt 0) {
            Start-Sleep -Milliseconds 500
        }
    }
    
    $runspacePool.Close()
    $runspacePool.Dispose()
    
    $batchEndTime = Get-Date
    $batchElapsed = $batchEndTime - $batchStartTime
    
    # Calculate batch results
    $batchTotalSent = ($batchResults | Measure-Object -Property Sent -Sum).Sum
    $batchTotalFailed = ($batchResults | Measure-Object -Property Failed -Sum).Sum
    $batchSuccessRate = if (($batchTotalSent + $batchTotalFailed) -gt 0) { 
        [Math]::Round(($batchTotalSent / ($batchTotalSent + $batchTotalFailed)) * 100, 2) 
    } else { 0 }
    
    $testResults["Batch"] = @{
        MessagesSent = $batchTotalSent
        MessagesFailed = $batchTotalFailed
        SuccessRate = $batchSuccessRate
        Duration = $batchElapsed
        Throughput = if ($batchElapsed.TotalSeconds -gt 0) { [Math]::Round($batchTotalSent / $batchElapsed.TotalSeconds, 2) } else { 0 }
    }
    
    Write-Host ""
    Write-Host "Batch Load Test Results:" -ForegroundColor Green
    Write-Host "  Messages Sent: $batchTotalSent" -ForegroundColor White
    Write-Host "  Messages Failed: $batchTotalFailed" -ForegroundColor White
    Write-Host "  Success Rate: $batchSuccessRate%" -ForegroundColor White
    Write-Host "  Duration: $($batchElapsed.ToString('hh\:mm\:ss'))" -ForegroundColor White
    Write-Host "  Throughput: $($testResults['Batch'].Throughput) messages/second" -ForegroundColor White
    Write-Host ""
}

if ($TestType -eq "full" -or $TestType -eq "nobatch-only") {
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "EXECUTING IMMEDIATE PROCESSING LOAD TEST" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "Messages: $NoBatchMessages" -ForegroundColor Yellow
    Write-Host "Producers: $($healthyProducers.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    # Prepare message jobs across all producers
    $allMessageJobs = @()
    for ($messageIndex = 1; $messageIndex -le $NoBatchMessages; $messageIndex++) {
        $producerIndex = ($messageIndex - 1) % $healthyProducers.Count
        $producerUrl = $healthyProducers[$producerIndex]
        
        $messageJob = @{
            ProducerUrl = $producerUrl
            MessageIndex = $messageIndex
            Topic = $Topic
            SessionId = $SessionId
            Verbose = $Verbose
        }
        
        $allMessageJobs += $messageJob
    }
    
    Write-Host "Starting immediate processing test with $($allMessageJobs.Count) messages..." -ForegroundColor Cyan
    $noBatchStartTime = Get-Date
    
    # Execute messages in parallel
    $noBatchResults = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $MaxConcurrentMessages)
    $runspacePool.Open()
    
    $messageFunction = {
        param($ProducerUrl, $MessageIndex, $Topic, $SessionId, $Verbose)
        
        $result = @{
            ProducerUrl = $ProducerUrl
            MessageIndex = $MessageIndex
            Success = $false
            Error = $null
            ResponseTime = 0
        }
        
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        try {
            $message = "SESSION:$SessionId | No-Batch Load Test Message $MessageIndex - Producer: $ProducerUrl"
            
            $body = @{
                Topic = $Topic
                Message = $message
                UseBatching = $false
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
            
            if ($response) {
                $result.Success = $true
            }
        }
        catch {
            $result.Error = $_.Exception.Message
        }
        finally {
            $stopwatch.Stop()
            $result.ResponseTime = $stopwatch.ElapsedMilliseconds
        }
        
        return $result
    }
    
    # Create and start message jobs
    $messageJobs = [System.Collections.ArrayList]@()
    foreach ($messageJob in $allMessageJobs) {
        $powerShell = [powershell]::Create()
        $powerShell.RunspacePool = $runspacePool
        
        [void]$powerShell.AddScript($messageFunction).AddParameters(@{
            ProducerUrl = $messageJob.ProducerUrl
            MessageIndex = $messageJob.MessageIndex
            Topic = $messageJob.Topic
            SessionId = $messageJob.SessionId
            Verbose = $messageJob.Verbose
        })
        
        [void]$messageJobs.Add(@{
            PowerShell = $powerShell
            Handle = $powerShell.BeginInvoke()
        })
    }
    
    # Monitor message completion
    $completedMessages = 0
    while ($messageJobs.Count -gt 0) {
        $completedIndices = @()
        
        for ($i = 0; $i -lt $messageJobs.Count; $i++) {
            if ($messageJobs[$i].Handle.IsCompleted) {
                try {
                    $result = $messageJobs[$i].PowerShell.EndInvoke($messageJobs[$i].Handle)
                    $noBatchResults.Add($result)
                    $completedMessages++
                    
                    if ($completedMessages % 250 -eq 0) {
                        $elapsed = (Get-Date) - $noBatchStartTime
                        Write-Host "Progress: $completedMessages/$($allMessageJobs.Count) messages completed - Elapsed: $($elapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Yellow
                    }
                    
                    $completedIndices += $i
                }
                catch {
                    Write-Warning "Error retrieving message result: $($_.Exception.Message)"
                    $completedIndices += $i
                }
                finally {
                    $messageJobs[$i].PowerShell.Dispose()
                }
            }
        }
        
        for ($i = $completedIndices.Count - 1; $i -ge 0; $i--) {
            $messageJobs.RemoveAt($completedIndices[$i])
        }
        
        if ($messageJobs.Count -gt 0) {
            Start-Sleep -Milliseconds 100
        }
    }
    
    $runspacePool.Close()
    $runspacePool.Dispose()
    
    $noBatchEndTime = Get-Date
    $noBatchElapsed = $noBatchEndTime - $noBatchStartTime
    
    # Calculate no-batch results
    $noBatchSuccessful = $noBatchResults | Where-Object { $_.Success -eq $true }
    $noBatchFailed = $noBatchResults | Where-Object { $_.Success -eq $false }
    $noBatchTotalSent = $noBatchSuccessful.Count
    $noBatchTotalFailed = $noBatchFailed.Count
    $noBatchSuccessRate = if (($noBatchTotalSent + $noBatchTotalFailed) -gt 0) { 
        [Math]::Round(($noBatchTotalSent / ($noBatchTotalSent + $noBatchTotalFailed)) * 100, 2) 
    } else { 0 }
    
    $testResults["NoBatch"] = @{
        MessagesSent = $noBatchTotalSent
        MessagesFailed = $noBatchTotalFailed
        SuccessRate = $noBatchSuccessRate
        Duration = $noBatchElapsed
        Throughput = if ($noBatchElapsed.TotalSeconds -gt 0) { [Math]::Round($noBatchTotalSent / $noBatchElapsed.TotalSeconds, 2) } else { 0 }
    }
    
    Write-Host ""
    Write-Host "Immediate Processing Test Results:" -ForegroundColor Green
    Write-Host "  Messages Sent: $noBatchTotalSent" -ForegroundColor White
    Write-Host "  Messages Failed: $noBatchTotalFailed" -ForegroundColor White
    Write-Host "  Success Rate: $noBatchSuccessRate%" -ForegroundColor White
    Write-Host "  Duration: $($noBatchElapsed.ToString('hh\:mm\:ss'))" -ForegroundColor White
    Write-Host "  Throughput: $($testResults['NoBatch'].Throughput) messages/second" -ForegroundColor White
    Write-Host ""
}

# Step 6: Verify Message Processing by Consumer Groups
Write-Host "================================================================" -ForegroundColor Green
Write-Host "VERIFYING MESSAGE PROCESSING BY CONSUMER GROUPS" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green

if ($healthyConsumers.Count -gt 0) {
    Write-Host "Waiting for message processing to complete..." -ForegroundColor Yellow
    Write-Host "This may take a few minutes for large message volumes..." -ForegroundColor Yellow
    Write-Host ""
    
    # Wait for processing with extended timeout for large volumes
    $totalMessages = 0
    if ($testResults.ContainsKey("Batch")) { $totalMessages += $testResults["Batch"].MessagesSent }
    if ($testResults.ContainsKey("NoBatch")) { $totalMessages += $testResults["NoBatch"].MessagesSent }
    
    # Dynamic timeout based on message volume
    $processingTimeout = [Math]::Max($VerificationTimeoutSeconds, $totalMessages / 1000)  # 1 second per 1000 messages minimum
    
    Write-Host "Processing verification timeout: $processingTimeout seconds" -ForegroundColor Yellow
    Write-Host "Total messages to verify: $totalMessages" -ForegroundColor Yellow
    Write-Host ""
    
    $verificationStart = Get-Date
    $consumerResults = @{}
    
    # Initialize tracking for each consumer group
    foreach ($group in $consumerGroups) {
        $consumerResults[$group.ConsumerGroupName] = @{
            ProcessedCount = 0
            FailedCount = 0
            LastUpdate = $verificationStart
        }
    }
    
    # Monitor processing with periodic reporting
    $reportInterval = 30  # Report every 30 seconds
    $lastReportTime = $verificationStart
    
    while ((Get-Date) - $verificationStart -lt [TimeSpan]::FromSeconds($processingTimeout)) {
        $currentTime = Get-Date
        
        # Check each consumer group for session-specific messages
        foreach ($group in $consumerGroups) {
            $groupName = $group.ConsumerGroupName
            $totalProcessed = 0
            $totalFailed = 0
            
            foreach ($consumerUrl in $healthyConsumers) {
                $processedMessages = Get-SessionProcessedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName -SessionId $SessionId -TestStartTime $TestStartTime
                $failedMessages = Get-SessionFailedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName -SessionId $SessionId -TestStartTime $TestStartTime
                
                $totalProcessed += $processedMessages.Count
                $totalFailed += $failedMessages.Count
            }
            
            # Update results if changed
            if ($totalProcessed -ne $consumerResults[$groupName].ProcessedCount -or $totalFailed -ne $consumerResults[$groupName].FailedCount) {
                $consumerResults[$groupName].ProcessedCount = $totalProcessed
                $consumerResults[$groupName].FailedCount = $totalFailed
                $consumerResults[$groupName].LastUpdate = $currentTime
            }
        }
        
        # Report progress every interval
        if (($currentTime - $lastReportTime).TotalSeconds -ge $reportInterval) {
            Write-Host "Processing Status (Session: $SessionId):" -ForegroundColor Cyan
            foreach ($group in $consumerGroups) {
                $groupName = $group.ConsumerGroupName
                $processed = $consumerResults[$groupName].ProcessedCount
                $failed = $consumerResults[$groupName].FailedCount
                $total = $processed + $failed
                Write-Host "  $groupName : $processed processed, $failed failed (Total: $total/$totalMessages)" -ForegroundColor White
            }
            Write-Host ""
            $lastReportTime = $currentTime
        }
        
        # Check if we have reasonable processing across all groups
        $totalProcessedAcrossGroups = 0
        foreach ($group in $consumerGroups) {
            $groupName = $group.ConsumerGroupName
            $totalProcessedAcrossGroups += $consumerResults[$groupName].ProcessedCount + $consumerResults[$groupName].FailedCount
        }
        
        # If we have processed most messages across all groups, we can conclude
        $expectedTotalProcessing = $totalMessages * $consumerGroups.Count
        if ($totalProcessedAcrossGroups -ge ($expectedTotalProcessing * 0.8)) {
            Write-Host "Sufficient message processing detected across all consumer groups!" -ForegroundColor Green
            break
        }
        
        Start-Sleep -Seconds 5
    }
    
    # Final results summary
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "FINAL E2E LOAD TEST RESULTS" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
    Write-Host "Test Duration: $((Get-Date) - $TestStartTime)" -ForegroundColor Yellow
    Write-Host ""
    
    # Load test summary
    if ($testResults.Count -gt 0) {
        Write-Host "Load Test Summary:" -ForegroundColor Cyan
        foreach ($testName in $testResults.Keys) {
            $result = $testResults[$testName]
            Write-Host "  $testName Test:" -ForegroundColor Yellow
            Write-Host "    Messages Sent: $($result.MessagesSent)" -ForegroundColor Green
            Write-Host "    Messages Failed: $($result.MessagesFailed)" -ForegroundColor Red
            Write-Host "    Success Rate: $($result.SuccessRate)%" -ForegroundColor White
            Write-Host "    Duration: $($result.Duration.ToString('hh\:mm\:ss'))" -ForegroundColor White
            Write-Host "    Throughput: $($result.Throughput) messages/second" -ForegroundColor White
            Write-Host ""
        }
    }
    
    # Consumer processing summary
    Write-Host "Consumer Processing Summary:" -ForegroundColor Cyan
    $totalConsumerProcessed = 0
    $totalConsumerFailed = 0
    
    foreach ($group in $consumerGroups) {
        $groupName = $group.ConsumerGroupName
        $processed = $consumerResults[$groupName].ProcessedCount
        $failed = $consumerResults[$groupName].FailedCount
        $total = $processed + $failed
        
        $totalConsumerProcessed += $processed
        $totalConsumerFailed += $failed
        
        Write-Host "  Consumer Group: $groupName" -ForegroundColor Yellow
        Write-Host "    Processed: $processed" -ForegroundColor Green
        Write-Host "    Failed: $failed" -ForegroundColor Red
        Write-Host "    Total: $total" -ForegroundColor White
        Write-Host ""
    }
    
    Write-Host "Overall Consumer Results:" -ForegroundColor Cyan
    Write-Host "  Total Processed: $totalConsumerProcessed" -ForegroundColor Green
    Write-Host "  Total Failed: $totalConsumerFailed" -ForegroundColor Red
    Write-Host "  Grand Total: $($totalConsumerProcessed + $totalConsumerFailed)" -ForegroundColor White
    
    # Success evaluation
    $expectedTotal = $totalMessages * $ConsumerGroups.Count
    $actualTotal = $totalConsumerProcessed + $totalConsumerFailed
    $processingRate = if ($expectedTotal -gt 0) { [Math]::Round(($actualTotal / $expectedTotal) * 100, 2) } else { 0 }
    
    Write-Host "  Processing Rate: $processingRate%" -ForegroundColor White
    Write-Host ""
    
    # Final assessment
    if ($processingRate -ge 90) {
        Write-Host "🎉 E2E LOAD TEST PASSED: Excellent message processing across all systems!" -ForegroundColor Green
    }
    elseif ($processingRate -ge 70) {
        Write-Host "✅ E2E LOAD TEST PASSED: Good message processing performance!" -ForegroundColor Green
    }
    elseif ($processingRate -ge 50) {
        Write-Host "⚠️ E2E LOAD TEST PARTIAL: Some message processing detected" -ForegroundColor Yellow
    }
    else {
        Write-Host "❌ E2E LOAD TEST FAILED: Poor message processing detected" -ForegroundColor Red
    }
}
else {
    Write-Host "⚠️ No consumer services available for verification!" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "E2E LOAD TEST COMPLETED" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green

Write-Host "Monitoring Commands:" -ForegroundColor Cyan
Write-Host "  • Monitor consumers: .\monitor-consumers.ps1" -ForegroundColor White
Write-Host "  • Check acknowledgments: .\verify-acknowledgments.ps1" -ForegroundColor White
Write-Host "  • Kafka UI: http://localhost:8080" -ForegroundColor White
Write-Host "  • Session ID: $SessionId" -ForegroundColor Magenta
Write-Host ""
Write-Host "Test completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
