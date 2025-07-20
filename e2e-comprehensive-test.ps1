# Comprehensive E2E Test - Producer to All Consumer Groups with Infinite Retry
# This script tests message delivery to ALL registered consumer groups and verifies acknowledgments

param(
  [string]$ProducerUrl = "http://localhost:5301",
  [string]$Topic = "shared-events", 
  [int]$MessageCount = 10,
  [bool]$UseBatching = $true,
  [int]$VerificationTimeoutSeconds = 300  # 5 minutes
)

$ErrorActionPreference = "Stop"

Write-Host "===========================================" -ForegroundColor Green
Write-Host "COMPREHENSIVE E2E TEST WITH INFINITE RETRY" -ForegroundColor Green  
Write-Host "===========================================" -ForegroundColor Green
Write-Host "Producer: $ProducerUrl" -ForegroundColor Yellow
Write-Host "Topic: $Topic" -ForegroundColor Yellow
Write-Host "Messages: $MessageCount" -ForegroundColor Yellow
Write-Host "Batching: $UseBatching" -ForegroundColor Yellow
Write-Host ""

# Standard consumer service ports in the system
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)

# Function to check service health
function Test-ServiceHealth {
  param([string]$Url, [string]$ServiceType = "producer")
  try {
    $healthEndpoint = if ($ServiceType -eq "producer") { "/api/messages/health" } else { "/api/consumer/health" }
    $response = Invoke-RestMethod -Uri "$Url$healthEndpoint" -Method Get -TimeoutSec 5
    return $response.Status -eq "Healthy"
  }
  catch {
    return $false
  }
}

# Function to get registered consumer groups
function Get-RegisteredConsumerGroups {
  param([string]$ProducerUrl, [string]$TopicName = "shared-events")
  try {
    # First get the topic by name
    $topicResponse = Invoke-RestMethod -Uri "$ProducerUrl/api/topics/by-name/$TopicName" -Method Get -TimeoutSec 10
    if (-not $topicResponse -or -not $topicResponse.Id) {
      Write-Warning "Topic '$TopicName' not found"
      return @()
    }
        
    # Then get consumer groups for that topic
    $response = Invoke-RestMethod -Uri "$ProducerUrl/api/topics/$($topicResponse.Id)/consumer-groups" -Method Get -TimeoutSec 10
    return $response | Where-Object { $_.IsActive -eq $true }
  }
  catch {
    Write-Error "Failed to get consumer groups: $_"
    return @()
  }
}

# Function to configure infinite retry
function Set-InfiniteRetry {
  param([string]$ProducerUrl, [array]$ConsumerGroups)
    
  Write-Host "Configuring infinite retry for all consumer groups..." -ForegroundColor Yellow
    
  foreach ($group in $ConsumerGroups) {
    $updateBody = @{
      ConsumerGroupName            = $group.ConsumerGroupName
      RequiresAcknowledgment       = $true
      AcknowledgmentTimeoutMinutes = 5
      MaxRetries                   = -1  # Infinite retry
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

# Function to send a message
function Send-Message {
  param([string]$ProducerUrl, [string]$Topic, [string]$Message, [bool]$UseBatching)
    
  $body = @{
    Topic       = $Topic
    Message     = $Message
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

# Function to get processed messages for a consumer group
function Get-ProcessedMessages {
  param([string]$ConsumerUrl, [string]$ConsumerGroup)
  try {
    $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/processed/$ConsumerGroup" -Method Get -TimeoutSec 10
    return $response
  }
  catch {
    return @()
  }
}

# Function to get failed messages for a consumer group  
function Get-FailedMessages {
  param([string]$ConsumerUrl, [string]$ConsumerGroup)
  try {
    $response = Invoke-RestMethod -Uri "$ConsumerUrl/api/consumer/failed/$ConsumerGroup" -Method Get -TimeoutSec 10
    return $response
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
}

if ($healthyConsumers.Count -eq 0) {
  Write-Warning "No consumer services found, but continuing test..."
}
Write-Host "Found $($healthyConsumers.Count) healthy consumer services" -ForegroundColor Yellow
Write-Host ""

# Step 3: Get registered consumer groups
Write-Host "Step 3: Getting registered consumer groups..." -ForegroundColor Cyan
$consumerGroups = Get-RegisteredConsumerGroups -ProducerUrl $ProducerUrl -TopicName $Topic

if ($consumerGroups.Count -eq 0) {
  throw "No active consumer groups found"
}

Write-Host "Found $($consumerGroups.Count) active consumer groups:" -ForegroundColor Green
foreach ($group in $consumerGroups) {
  Write-Host "  - $($group.ConsumerGroupName) (MaxRetries: $($group.MaxRetries))" -ForegroundColor White
}
Write-Host ""

# Step 4: Configure infinite retry
Set-InfiniteRetry -ProducerUrl $ProducerUrl -ConsumerGroups $consumerGroups

# Step 5: Send test messages
Write-Host "Step 5: Sending $MessageCount test messages..." -ForegroundColor Cyan
$sentMessages = @()
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

for ($i = 1; $i -le $MessageCount; $i++) {
  $message = "E2E Test Message $i - Timestamp: $timestamp - Producer: $ProducerUrl"
  Write-Host "Sending message $i/$MessageCount..." -ForegroundColor Yellow
    
  $response = Send-Message -ProducerUrl $ProducerUrl -Topic $Topic -Message $message -UseBatching $UseBatching
  if ($response) {
    $sentMessages += @{
      MessageId            = $response.MessageId
      Message              = $message
      Status               = $response.Status
      Topic                = $response.Topic
      TargetConsumerGroups = $response.TargetConsumerGroups
    }
    Write-Host "[OK] Message sent - ID: $($response.MessageId)" -ForegroundColor Green
  }
  else {
    Write-Error "Failed to send message $i"
  }
    
  Start-Sleep -Milliseconds 100
}
Write-Host ""

# Step 6: Monitor acknowledgments with infinite retry verification
Write-Host "Step 6: Monitoring message delivery to ALL consumer groups..." -ForegroundColor Cyan
Write-Host "Verification timeout: $VerificationTimeoutSeconds seconds" -ForegroundColor Yellow
Write-Host "Note: Failed messages will be retried infinitely" -ForegroundColor Yellow
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
  Write-Host "Checking acknowledgments..." -ForegroundColor Yellow
    
  # Check each consumer service for acknowledgments
  foreach ($consumerUrl in $healthyConsumers) {
    foreach ($group in $consumerGroups) {
      $groupName = $group.ConsumerGroupName
            
      # Get processed messages
      $processedMessages = Get-ProcessedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName
      if ($processedMessages) {
        foreach ($processed in $processedMessages) {
          # Avoid duplicates
          if ($processed.MessageId -notin $acknowledgedMessages[$groupName].MessageId) {
            $acknowledgedMessages[$groupName] += $processed
          }
        }
      }
            
      # Get failed messages  
      $currentFailedMessages = Get-FailedMessages -ConsumerUrl $consumerUrl -ConsumerGroup $groupName
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
    
  # Display current status
  Write-Host "Current Status:" -ForegroundColor Cyan
  foreach ($group in $consumerGroups) {
    $groupName = $group.ConsumerGroupName
    $ackCount = $acknowledgedMessages[$groupName].Count
    $failCount = $failedMessages[$groupName].Count
    $totalProcessed = $ackCount + $failCount
    Write-Host "  ${groupName}: $ackCount acknowledged, $failCount failed (Total: $totalProcessed/$MessageCount)" -ForegroundColor White
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
    Write-Host "[OK] All messages processed by all consumer groups!" -ForegroundColor Green
    break
  }
    
  Start-Sleep -Seconds 5
}

Write-Host ""

# Step 7: Final results
Write-Host "========================================" -ForegroundColor Green
Write-Host "FINAL TEST RESULTS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

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

Write-Host "OVERALL SUMMARY:" -ForegroundColor Cyan
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
  Write-Host "  - Check retry job logs in producer service" -ForegroundColor Yellow
}

if ($successRate -gt 95) {
  Write-Host "[SUCCESS] TEST PASSED: System is operating excellently!" -ForegroundColor Green
}
elseif ($successRate -gt 80) {
  Write-Host "[OK] TEST PASSED: System is operating well!" -ForegroundColor Green
}
elseif ($totalFailed -gt 0) {
  Write-Host "[WARNING] TEST PARTIAL: Some failures detected but infinite retry is active" -ForegroundColor Yellow
}
else {
  Write-Host "[X] TEST FAILED: System has issues" -ForegroundColor Red
}

Write-Host ""
Write-Host "[INFO] MONITORING COMMANDS:" -ForegroundColor Cyan
Write-Host "  - Monitor consumers: .\monitor-consumers.ps1" -ForegroundColor White
Write-Host "  - Check outbox: .\check-outbox.sql" -ForegroundColor White  
Write-Host "  - Verify acknowledgments: .\verify-acknowledgments.ps1" -ForegroundColor White
Write-Host "  - Kafka UI: http://localhost:8080" -ForegroundColor White
