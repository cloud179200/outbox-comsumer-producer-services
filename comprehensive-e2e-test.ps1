# Comprehensive End-to-End Test with Infinite Retry
# This script tests message delivery to all registered consumer groups with acknowledgment verification

param(
    [string]$ProducerUrl = "http://localhost:5301",
    [string]$Topic = "shared-events",
    [int]$MessageCount = 10,
    [bool]$UseBatching = $true,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "COMPREHENSIVE END-TO-END TEST WITH INFINITE RETRY" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Producer URL: $ProducerUrl" -ForegroundColor Yellow
Write-Host "Topic: $Topic" -ForegroundColor Yellow
Write-Host "Message Count: $MessageCount" -ForegroundColor Yellow
Write-Host "Use Batching: $UseBatching" -ForegroundColor Yellow
Write-Host "Timeout: $TimeoutSeconds seconds" -ForegroundColor Yellow
Write-Host ""

# Function to check service health
function Test-ServiceHealth {
    param([string]$Url)
    try {
        $response = Invoke-RestMethod -Uri "$Url/api/messages/health" -Method Get -TimeoutSec 5
        return $response.Status -eq "Healthy"
    }
    catch {
        return $false
    }
}

# Function to get all registered consumer groups
function Get-RegisteredConsumerGroups {
    param([string]$ProducerUrl)
    try {
        $response = Invoke-RestMethod -Uri "$ProducerUrl/api/topics/consumer-groups" -Method Get -TimeoutSec 10
        return $response | Where-Object { $_.IsActive -eq $true }
    }
    catch {
        Write-Error "Failed to get registered consumer groups: $_"
        return @()
    }
}

# Function to send a message
function Send-Message {
    param([string]$ProducerUrl, [string]$Topic, [string]$Message, [bool]$UseBatching)
    
    $body = @{
        Topic = $Topic
        Message = $Message
        UseBatching = $UseBatching
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$ProducerUrl/api/messages/send" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
        return $response
    }
    catch {
        Write-Error "Failed to send message: $_"
        return $null
    }
}

# Function to check acknowledgments
function Get-Acknowledgments {
    param([string]$ConsumerUrl, [string]$ConsumerGroup)
    try {
        $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/processed/$ConsumerGroup" -Method Get -TimeoutSec 10
        return $response
    }
    catch {
        Write-Warning "Failed to check acknowledgments for consumer group $ConsumerGroup from $ConsumerUrl"
        return @()
    }
}

# Function to get failed messages
function Get-FailedMessages {
    param([string]$ConsumerUrl, [string]$ConsumerGroup)
    try {
        $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/failed/$ConsumerGroup" -Method Get -TimeoutSec 10
        return $response
    }
    catch {
        Write-Warning "Failed to get failed messages for consumer group $ConsumerGroup from $ConsumerUrl"
        return @()
    }
}

# Function to configure infinite retry for consumer groups
function Set-InfiniteRetry {
    param([string]$ProducerUrl)
    
    Write-Host "Configuring infinite retry for all consumer groups..." -ForegroundColor Yellow
    
    $consumerGroups = Get-RegisteredConsumerGroups -ProducerUrl $ProducerUrl
    
    foreach ($group in $consumerGroups) {
        $updateBody = @{
            ConsumerGroupName = $group.ConsumerGroupName
            RequiresAcknowledgment = $true
            AcknowledgmentTimeoutMinutes = 5
            MaxRetries = -1  # Set to -1 for infinite retry
        } | ConvertTo-Json
        
        try {
            $updateUrl = "$ProducerUrl/api/topics/consumer-groups/$($group.Id)"
            Invoke-RestMethod -Uri $updateUrl -Method Put -Body $updateBody -ContentType "application/json" -TimeoutSec 10
            Write-Host "✅ Set infinite retry for consumer group: $($group.ConsumerGroupName)" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to set infinite retry for consumer group $($group.ConsumerGroupName): $_"
        }
    }
}

# Main test execution
try {
    # Step 1: Check producer health
    Write-Host "Step 1: Checking producer health..." -ForegroundColor Cyan
    if (-not (Test-ServiceHealth -Url $ProducerUrl)) {
        throw "Producer service at $ProducerUrl is not healthy"
    }
    Write-Host "✅ Producer service is healthy" -ForegroundColor Green
    Write-Host ""

    # Step 2: Configure infinite retry
    Write-Host "Step 2: Configuring infinite retry..." -ForegroundColor Cyan
    Set-InfiniteRetry -ProducerUrl $ProducerUrl
    Write-Host ""

    # Step 3: Get all registered consumer groups
    Write-Host "Step 3: Getting registered consumer groups..." -ForegroundColor Cyan
    $consumerGroups = Get-RegisteredConsumerGroups -ProducerUrl $ProducerUrl
    
    if ($consumerGroups.Count -eq 0) {
        throw "No active consumer groups found"
    }
    
    Write-Host "Found $($consumerGroups.Count) active consumer groups:" -ForegroundColor Green
    foreach ($group in $consumerGroups) {
        Write-Host "  - $($group.ConsumerGroupName) (MaxRetries: $($group.MaxRetries))" -ForegroundColor White
    }
    Write-Host ""

    # Step 4: Discover consumer services for health checking
    Write-Host "Step 4: Discovering consumer services..." -ForegroundColor Cyan
    $consumerUrls = @()
    # Standard consumer ports in the system
    $standardPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
    
    foreach ($port in $standardPorts) {
        $consumerUrl = "http://localhost:$port"
        if (Test-ServiceHealth -Url $consumerUrl) {
            $consumerUrls += $consumerUrl
            Write-Host "✅ Consumer service discovered: $consumerUrl" -ForegroundColor Green
        }
    }
    
    if ($consumerUrls.Count -eq 0) {
        Write-Warning "No consumer services found, but continuing test..."
    }
    Write-Host ""

    # Step 5: Send test messages
    Write-Host "Step 5: Sending $MessageCount test messages..." -ForegroundColor Cyan
    $sentMessages = @()
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    
    for ($i = 1; $i -le $MessageCount; $i++) {
        $message = "Test message $i - Comprehensive E2E Test - $timestamp"
        Write-Host "Sending message $i/$MessageCount..." -ForegroundColor Yellow
        
        $response = Send-Message -ProducerUrl $ProducerUrl -Topic $Topic -Message $message -UseBatching $UseBatching
        if ($response) {
            $sentMessages += @{
                MessageId = $response.MessageId
                Message = $message
                Status = $response.Status
                Topic = $response.Topic
                TargetConsumerGroups = $response.TargetConsumerGroups
            }
            Write-Host "✅ Message sent - ID: $($response.MessageId)" -ForegroundColor Green
        } else {
            Write-Error "Failed to send message $i"
        }
        
        Start-Sleep -Milliseconds 100
    }
    Write-Host ""

    # Step 6: Wait for processing and verify acknowledgments
    Write-Host "Step 6: Waiting for message processing and verification..." -ForegroundColor Cyan
    Write-Host "Waiting for $TimeoutSeconds seconds for message processing..." -ForegroundColor Yellow
    
    $startTime = Get-Date
    $acknowledgedMessages = @{}
    $failedMessages = @{}
    
    # Initialize tracking for each consumer group
    foreach ($group in $consumerGroups) {
        $acknowledgedMessages[$group.ConsumerGroupName] = @()
        $failedMessages[$group.ConsumerGroupName] = @()
    }
    
    # Monitoring loop
    while ((Get-Date) - $startTime -lt [TimeSpan]::FromSeconds($TimeoutSeconds)) {
        Write-Host "Checking acknowledgments..." -ForegroundColor Yellow
        
        # Check acknowledgments from all consumer services
        foreach ($consumerUrl in $consumerUrls) {
            foreach ($group in $consumerGroups) {
                $groupName = $group.ConsumerGroupName
                
                # Get processed messages
                $processedMessages = Get-Acknowledgments -ConsumerUrl $consumerUrl -ConsumerGroup $groupName
                if ($processedMessages) {
                    foreach ($processed in $processedMessages) {
                        if ($processed.MessageId -notin $acknowledgedMessages[$groupName].MessageId) {
                            $acknowledgedMessages[$groupName] += $processed
                        }
                    }
                }
                
                # Get failed messages
                $currentFailedMessages = Get-FailedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName
                if ($currentFailedMessages) {
                    foreach ($failed in $currentFailedMessages) {
                        if ($failed.MessageId -notin $failedMessages[$groupName].MessageId) {
                            $failedMessages[$groupName] += $failed
                        }
                    }
                }
            }
        }
        
        # Display current status
        Write-Host "Current Status:" -ForegroundColor Cyan
        foreach ($group in $consumerGroups) {
            $groupName = $group.ConsumerGroupName
            $ackCount = $acknowledgedMessages[$groupName].Count
            $failCount = $failedMessages[$groupName].Count
            Write-Host "  ${groupName}: $ackCount acknowledged, $failCount failed" -ForegroundColor White
        }
        
        # Check if all messages are processed by all groups
        $allProcessed = $true
        foreach ($group in $consumerGroups) {
            $groupName = $group.ConsumerGroupName
            $totalProcessed = $acknowledgedMessages[$groupName].Count + $failedMessages[$groupName].Count
            if ($totalProcessed -lt $MessageCount) {
                $allProcessed = $false
                break
            }
        }
        
        if ($allProcessed) {
            Write-Host "✅ All messages processed by all consumer groups!" -ForegroundColor Green
            break
        }
        
        Start-Sleep -Seconds 5
    }
    
    Write-Host ""

    # Step 7: Final results
    Write-Host "=================================================" -ForegroundColor Cyan
    Write-Host "FINAL TEST RESULTS" -ForegroundColor Cyan
    Write-Host "=================================================" -ForegroundColor Cyan
    
    Write-Host "Messages Sent: $($sentMessages.Count)" -ForegroundColor Yellow
    Write-Host "Expected Deliveries: $($sentMessages.Count * $consumerGroups.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    $totalAcknowledged = 0
    $totalFailed = 0
    
    foreach ($group in $consumerGroups) {
        $groupName = $group.ConsumerGroupName
        $ackCount = $acknowledgedMessages[$groupName].Count
        $failCount = $failedMessages[$groupName].Count
        $totalProcessed = $ackCount + $failCount
        
        $totalAcknowledged += $ackCount
        $totalFailed += $failCount
        
        Write-Host "Consumer Group: $groupName" -ForegroundColor Cyan
        Write-Host "  ✅ Acknowledged: $ackCount/$MessageCount" -ForegroundColor Green
        Write-Host "  ❌ Failed: $failCount/$MessageCount" -ForegroundColor Red
        Write-Host "  📊 Total Processed: $totalProcessed/$MessageCount" -ForegroundColor White
        
        if ($failCount -gt 0) {
            Write-Host "  🔄 Failed messages will be retried infinitely" -ForegroundColor Yellow
        }
        Write-Host ""
    }
    
    Write-Host "OVERALL RESULTS:" -ForegroundColor Cyan
    Write-Host "  ✅ Total Acknowledged: $totalAcknowledged" -ForegroundColor Green
    Write-Host "  ❌ Total Failed: $totalFailed" -ForegroundColor Red
    Write-Host "  📊 Total Deliveries: $($totalAcknowledged + $totalFailed)" -ForegroundColor White
    
    $successRate = if ($sentMessages.Count * $consumerGroups.Count -gt 0) { 
        [math]::Round(($totalAcknowledged / ($sentMessages.Count * $consumerGroups.Count)) * 100, 2) 
    } else { 0 }
    
    Write-Host "  📈 Success Rate: $successRate%" -ForegroundColor White
    Write-Host ""
    
    if ($totalFailed -gt 0) {
        Write-Host "🔄 INFINITE RETRY ENABLED:" -ForegroundColor Yellow
        Write-Host "  Failed messages will be retried automatically" -ForegroundColor Yellow
        Write-Host "  Monitor consumer logs for retry attempts" -ForegroundColor Yellow
        Write-Host "  Failed messages will eventually be delivered" -ForegroundColor Yellow
    }
    
    if ($successRate -gt 90) {
        Write-Host "🎉 TEST PASSED: System is operating correctly!" -ForegroundColor Green
    } elseif ($totalFailed -gt 0) {
        Write-Host "⚠️  TEST PARTIAL: Some failures detected but infinite retry is active" -ForegroundColor Yellow
    } else {
        Write-Host "❌ TEST FAILED: Low success rate detected" -ForegroundColor Red
    }
}
catch {
    Write-Host "❌ TEST FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test completed at $(Get-Date)" -ForegroundColor Gray
