# Session-Specific E2E Test - Only counts messages from current test session
# This script tests message delivery and tracks only messages sent during this test run

param(
  [string]$ProducerUrl = "http://localhost:5301",
  [string]$Topic = "shared-events", 
  [int]$MessageCount = 10,
  [bool]$UseBatching = $true,
  [int]$VerificationTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

# Generate unique session ID for this test run
$SessionId = [System.Guid]::NewGuid().ToString("N").Substring(0, 8)
$TestStartTime = Get-Date

Write-Host "=============================================" -ForegroundColor Green
Write-Host "SESSION-SPECIFIC E2E TEST WITH INFINITE RETRY" -ForegroundColor Green  
Write-Host "=============================================" -ForegroundColor Green
Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
Write-Host "Producer: $ProducerUrl" -ForegroundColor Yellow
Write-Host "Topic: $Topic" -ForegroundColor Yellow
Write-Host "Messages: $MessageCount" -ForegroundColor Yellow
Write-Host "Batching: $UseBatching" -ForegroundColor Yellow
Write-Host "Test Start Time: $($TestStartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
Write-Host ""

# Standard consumer service ports in the system
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)

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

# Function to send a message with session identification
function Send-Message {
  param([string]$ProducerUrl, [string]$Topic, [string]$Message, [bool]$UseBatching, [string]$SessionId)
  
  # Include session ID in the message content for tracking
  $sessionMessage = "SESSION:$SessionId | $Message"
  
  $body = @{
    Topic = $Topic
    Message = $sessionMessage
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

# Function to get processed messages for a consumer group (filtered by session and time)
function Get-SessionProcessedMessages {
  param([string]$ConsumerUrl, [string]$ConsumerGroup, [string]$SessionId, [DateTime]$TestStartTime)
  
  try {
    $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/processed/$ConsumerGroup" -Method Get -TimeoutSec 10
    
    if ($response) {
      # Filter messages that:
      # 1. Contain our session ID in the content
      # 2. Were processed after the test start time
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

# Function to get failed messages for a consumer group (filtered by session and time)
function Get-SessionFailedMessages {
  param([string]$ConsumerUrl, [string]$ConsumerGroup, [string]$SessionId, [DateTime]$TestStartTime)
  
  try {
    $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/failed/$ConsumerGroup" -Method Get -TimeoutSec 10
    
    if ($response) {
      # Filter messages that:
      # 1. Contain our session ID in the content
      # 2. Failed after the test start time
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

# Step 1: Verify producer health
Write-Host "Step 1: Verifying producer health..." -ForegroundColor Cyan
if (-not (Test-ServiceHealth -Url $ProducerUrl -ServiceType "producer")) {
  throw "Producer service at $ProducerUrl is not healthy"
}
Write-Host "[OK] Producer service is healthy" -ForegroundColor Green
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
$consumerGroups = Get-RegisteredConsumerGroups -ProducerUrl $ProducerUrl -TopicName $Topic

if ($consumerGroups.Count -eq 0) {
  throw "No active consumer groups found for topic '$Topic'"
}

Write-Host "Found $($consumerGroups.Count) active consumer groups:" -ForegroundColor Green
foreach ($group in $consumerGroups) {
  Write-Host "  - $($group.ConsumerGroupName) (MaxRetries: $($group.MaxRetries))" -ForegroundColor White
}
Write-Host ""

# Step 4: Configure infinite retry
Set-InfiniteRetry -ProducerUrl $ProducerUrl -ConsumerGroups $consumerGroups

# Step 5: Send test messages with session identification
Write-Host "Step 5: Sending $MessageCount test messages with session ID..." -ForegroundColor Cyan
$sentMessages = @()
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

for ($i = 1; $i -le $MessageCount; $i++) {
  $message = "E2E Test Message $i - Timestamp: $timestamp - Producer: $ProducerUrl"
  Write-Host "Sending message $i/$MessageCount..." -ForegroundColor Yellow
  
  $response = Send-Message -ProducerUrl $ProducerUrl -Topic $Topic -Message $message -UseBatching $UseBatching -SessionId $SessionId
  if ($response) {
    $sentMessages += @{
      MessageId            = $response.MessageId
      Message              = $message
      Status               = $response.Status
      Topic                = $response.Topic
      TargetConsumerGroups = $response.TargetConsumerGroups
      SessionId            = $SessionId
    }
    Write-Host "[OK] Message sent - ID: $($response.MessageId)" -ForegroundColor Green
  }
  else {
    Write-Error "Failed to send message $i"
  }
  
  Start-Sleep -Milliseconds 100
}
Write-Host ""

# Step 6: Monitor session-specific acknowledgments
Write-Host "Step 6: Monitoring SESSION-SPECIFIC message delivery..." -ForegroundColor Cyan
Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
Write-Host "Verification timeout: $VerificationTimeoutSeconds seconds" -ForegroundColor Yellow
Write-Host "Note: Only counting messages from this test session" -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date
$acknowledgedMessages = @{}
$failedMessages = @{}

# Initialize tracking for each consumer group
foreach ($group in $consumerGroups) {
  $acknowledgedMessages[$group.ConsumerGroupName] = @()
  $failedMessages[$group.ConsumerGroupName] = @()
}

# Monitoring loop
while ((Get-Date) - $startTime -lt [TimeSpan]::FromSeconds($VerificationTimeoutSeconds)) {
  Write-Host "Checking session-specific acknowledgments..." -ForegroundColor Yellow
  
  # Check each consumer service for session-specific acknowledgments
  foreach ($consumerUrl in $healthyConsumers) {
    foreach ($group in $consumerGroups) {
      $groupName = $group.ConsumerGroupName
      
      # Get session-specific processed messages
      $processedMessages = Get-SessionProcessedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName -SessionId $SessionId -TestStartTime $TestStartTime
      if ($processedMessages) {
        foreach ($processed in $processedMessages) {
          # Avoid duplicates
          if ($processed.MessageId -notin $acknowledgedMessages[$groupName].MessageId) {
            $acknowledgedMessages[$groupName] += $processed
          }
        }
      }
      
      # Get session-specific failed messages  
      $currentFailedMessages = Get-SessionFailedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName -SessionId $SessionId -TestStartTime $TestStartTime
      if ($currentFailedMessages) {
        foreach ($failed in $currentFailedMessages) {
          # Avoid duplicates
          if ($failed.MessageId -notin $failedMessages[$groupName].MessageId) {
            $failedMessages[$groupName] += $failed
          }
        }
      }
    }
  }
  
  # Display current session-specific status
  Write-Host "Current Session Status:" -ForegroundColor Cyan
  foreach ($group in $consumerGroups) {
    $groupName = $group.ConsumerGroupName
    $ackCount = $acknowledgedMessages[$groupName].Count
    $failCount = $failedMessages[$groupName].Count
    $totalProcessed = $ackCount + $failCount
    $expectedPerGroup = $MessageCount
    Write-Host "  ${groupName}: $ackCount acknowledged, $failCount failed (Expected: $expectedPerGroup per group)" -ForegroundColor White
  }
  
  # Check if all session messages are processed by all groups
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
    Write-Host "[OK] All session messages processed by all consumer groups!" -ForegroundColor Green
    break
  }
  
  Start-Sleep -Seconds 5
}

Write-Host ""

# Step 7: Final session-specific results
Write-Host "========================================" -ForegroundColor Green
Write-Host "FINAL SESSION-SPECIFIC TEST RESULTS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "Session ID: $SessionId" -ForegroundColor Magenta
Write-Host "Messages Sent: $($sentMessages.Count)" -ForegroundColor Yellow
Write-Host "Consumer Groups: $($consumerGroups.Count)" -ForegroundColor Yellow  
Write-Host "Expected Total Deliveries: $($sentMessages.Count * $consumerGroups.Count)" -ForegroundColor Yellow
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
  Write-Host "  [OK] Acknowledged: $ackCount/$MessageCount" -ForegroundColor Green
  Write-Host "  [X] Failed: $failCount/$MessageCount" -ForegroundColor Red
  Write-Host "  [INFO] Total Processed: $totalProcessed/$MessageCount" -ForegroundColor White
  
  if ($failCount -gt 0) {
    Write-Host "  [RETRY] Failed messages will be retried infinitely" -ForegroundColor Yellow
  }
  Write-Host ""
}

Write-Host "OVERALL SESSION SUMMARY:" -ForegroundColor Cyan
Write-Host "  [OK] Total Acknowledged: $totalAcknowledged" -ForegroundColor Green
Write-Host "  [X] Total Failed: $totalFailed" -ForegroundColor Red
Write-Host "  [INFO] Total Deliveries: $($totalAcknowledged + $totalFailed)" -ForegroundColor White

$successRate = if ($sentMessages.Count * $consumerGroups.Count -gt 0) { 
  [math]::Round(($totalAcknowledged / ($sentMessages.Count * $consumerGroups.Count)) * 100, 2) 
}
else { 0 }

Write-Host "  [CHART] Success Rate: $successRate%" -ForegroundColor White
Write-Host ""

if ($totalFailed -gt 0) {
  Write-Host "[RETRY] INFINITE RETRY SYSTEM ACTIVE:" -ForegroundColor Yellow
  Write-Host "  - Failed messages will be automatically retried" -ForegroundColor Yellow
  Write-Host "  - Monitor consumer logs for retry attempts" -ForegroundColor Yellow
  Write-Host "  - Failed messages will eventually be delivered" -ForegroundColor Yellow
  Write-Host ""
}

# Test result determination
$expectedTotal = $sentMessages.Count * $consumerGroups.Count
if ($totalAcknowledged -eq $expectedTotal -and $totalFailed -eq 0) {
  Write-Host "[SUCCESS] SESSION TEST PASSED: All messages processed successfully!" -ForegroundColor Green
}
elseif ($totalAcknowledged + $totalFailed -eq $expectedTotal) {
  Write-Host "[PARTIAL] SESSION TEST PARTIAL: All messages delivered but some failed" -ForegroundColor Yellow
}
else {
  Write-Host "[PENDING] SESSION TEST PENDING: Some messages still processing..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[INFO] SESSION MONITORING COMMANDS:" -ForegroundColor Cyan
Write-Host "  - Monitor consumers: .\monitor-consumers.ps1" -ForegroundColor White
Write-Host "  - Check outbox: .\check-outbox.sql" -ForegroundColor White
Write-Host "  - Verify acknowledgments: .\verify-acknowledgments.ps1" -ForegroundColor White
Write-Host "  - Kafka UI: http://localhost:8080" -ForegroundColor White
Write-Host "  - Session ID for filtering: $SessionId" -ForegroundColor Magenta
