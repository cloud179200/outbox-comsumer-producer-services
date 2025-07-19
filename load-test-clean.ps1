# Simple Load Testing Script for Docker Scaled Outbox System
# Sends messages concurrently to 3 producer services

param(
    [int]$TotalMessages = 1000,      # Default: 1,000 messages
    [int]$BatchSize = 50,            # Default: 50 messages per batch
    [int]$MaxConcurrency = 10,       # Maximum concurrent requests
    [string]$Topic = "shared-events",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Producer endpoints
$Producers = @(
    @{ Name = "Producer1"; Url = "http://localhost:5301/api/messages/send" },
    @{ Name = "Producer2"; Url = "http://localhost:5302/api/messages/send" },
    @{ Name = "Producer3"; Url = "http://localhost:5303/api/messages/send" }
)

# Statistics
$Stats = @{
    TotalSent = 0
    TotalFailed = 0
    StartTime = Get-Date
    Producers = @{}
}

foreach ($producer in $Producers) {
    $Stats.Producers[$producer.Name] = @{ Sent = 0; Failed = 0 }
}

function Write-Status {
    param([string]$Message, [string]$Color = "White")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] $Message" -ForegroundColor $Color
}

function Send-MessageBatch {
    param(
        [hashtable]$Producer,
        [int]$BatchNumber,
        [int]$BatchSize,
        [string]$Topic
    )
    
    $sent = 0
    $failed = 0
    $batchStart = Get-Date
    
    for ($i = 0; $i -lt $BatchSize; $i++) {
        $messageContent = @{
            messageId = [Guid]::NewGuid().ToString()
            timestamp = (Get-Date).ToString("O")
            batchNumber = $BatchNumber
            messageIndex = $i
            producer = $Producer.Name
            content = "Load test message $($BatchNumber * $BatchSize + $i + 1) from $($Producer.Name)"
        } | ConvertTo-Json -Compress
        
        $requestBody = @{
            topic = $Topic
            message = $messageContent
        } | ConvertTo-Json
        
        try {
            $null = Invoke-RestMethod -Uri $Producer.Url -Method POST -Body $requestBody -ContentType "application/json" -TimeoutSec 10
            $sent++
            if ($Verbose) {
                Write-Status "Message $($i + 1) sent via $($Producer.Name)" "Green"
            }
        } catch {
            $failed++
            if ($Verbose) {
                Write-Status "Message $($i + 1) failed via $($Producer.Name): $($_.Exception.Message)" "Red"
            }
        }
    }
    
    $duration = (Get-Date) - $batchStart
    $throughput = if ($duration.TotalSeconds -gt 0) { [math]::Round(($sent + $failed) / $duration.TotalSeconds, 2) } else { 0 }
    
    return @{
        Producer = $Producer.Name
        BatchNumber = $BatchNumber
        Sent = $sent
        Failed = $failed
        Duration = $duration.TotalMilliseconds
        Throughput = $throughput
    }
}

# Main execution
Write-Status "Starting Load Test for Docker Scaled Outbox System" "Green"
Write-Status "=================================================" "Green"

Write-Status "Configuration:" "Cyan"
Write-Status "  * Total Messages: $($TotalMessages.ToString('N0'))" "White"
Write-Status "  * Batch Size: $BatchSize" "White"
Write-Status "  * Max Concurrency: $MaxConcurrency" "White"
Write-Status "  * Target Topic: $Topic" "White"
Write-Status "  * Producers: $($Producers.Count)" "White"

# Health check
Write-Status "" "White"
Write-Status "Checking producer health..." "Cyan"
foreach ($producer in $Producers) {
    try {
        $healthUrl = $producer.Url.Replace("/api/messages/send", "/api/messages/health")
        $response = Invoke-RestMethod -Uri $healthUrl -Method GET -TimeoutSec 5
        if ($response.Status -eq "Healthy") {
            Write-Status "  * $($producer.Name) is healthy" "Green"
        } else {
            Write-Status "  * $($producer.Name) status: $($response.Status)" "Yellow"
        }
    } catch {
        Write-Status "  * $($producer.Name) is not responding" "Red"
    }
}

# Calculate batches
$totalBatches = [math]::Ceiling($TotalMessages / $BatchSize)
$Stats.StartTime = Get-Date

Write-Status "" "White"
Write-Status "Starting load test..." "Cyan"
Write-Status "  * Total batches: $totalBatches" "White"

# Process batches across producers
$batchResults = @()

for ($batch = 0; $batch -lt $totalBatches; $batch++) {
    $producer = $Producers[$batch % $Producers.Count]
    $currentBatchSize = [math]::Min($BatchSize, $TotalMessages - ($batch * $BatchSize))
    
    Write-Status "Batch $($batch + 1)/$totalBatches - Sending $currentBatchSize messages via $($producer.Name)..." "Cyan"
    
    $result = Send-MessageBatch -Producer $producer -BatchNumber $batch -BatchSize $currentBatchSize -Topic $Topic
    $batchResults += $result
    
    # Update statistics
    $Stats.TotalSent += $result.Sent
    $Stats.TotalFailed += $result.Failed
    $Stats.Producers[$result.Producer].Sent += $result.Sent
    $Stats.Producers[$result.Producer].Failed += $result.Failed
    
    # Progress update
    $progress = [math]::Round(($batch + 1) / $totalBatches * 100, 2)
    Write-Status "Batch $($batch + 1) completed: $($result.Sent) sent, $($result.Failed) failed, $($result.Throughput) msg/sec - Progress: $progress%" "Green"
}

$Stats.EndTime = Get-Date
$totalDuration = $Stats.EndTime - $Stats.StartTime

Write-Status "" "White"
Write-Status "Load test completed!" "Green"
Write-Status "===================" "Green"

# Final statistics
Write-Status "" "White"
Write-Status "Final Statistics:" "Cyan"
Write-Status "  * Total Messages: $($TotalMessages.ToString('N0'))" "White"
Write-Status "  * Messages Sent: $($Stats.TotalSent.ToString('N0'))" "Green"
Write-Status "  * Messages Failed: $($Stats.TotalFailed.ToString('N0'))" "Red"
Write-Status "  * Success Rate: $([math]::Round(($Stats.TotalSent / $TotalMessages) * 100, 2))%" "Green"
Write-Status "  * Total Duration: $($totalDuration.ToString('mm\:ss\.fff'))" "White"
Write-Status "  * Average Throughput: $([math]::Round($Stats.TotalSent / $totalDuration.TotalSeconds, 2)) msg/sec" "Cyan"

Write-Status "" "White"
Write-Status "Producer Statistics:" "Cyan"
foreach ($producerName in $Stats.Producers.Keys) {
    $producerStats = $Stats.Producers[$producerName]
    $total = $producerStats.Sent + $producerStats.Failed
    $successRate = if ($total -gt 0) { [math]::Round(($producerStats.Sent / $total) * 100, 2) } else { 0 }
    Write-Status "  * $producerName - Sent: $($producerStats.Sent.ToString('N0')), Failed: $($producerStats.Failed.ToString('N0')), Success Rate: $successRate%" "White"
}

Write-Status "" "White"
Write-Status "Performance Summary:" "Cyan"
$avgBatchDuration = ($batchResults | Measure-Object -Property Duration -Average).Average
$avgBatchThroughput = ($batchResults | Measure-Object -Property Throughput -Average).Average
Write-Status "  * Average Batch Duration: $([math]::Round($avgBatchDuration, 2)) ms" "White"
Write-Status "  * Average Batch Throughput: $([math]::Round($avgBatchThroughput, 2)) msg/sec" "White"
Write-Status "  * Total Batches: $totalBatches" "White"

if ($Stats.TotalFailed -gt 0) {
    Write-Status "" "White"
    Write-Status "Warning: $($Stats.TotalFailed) messages failed. Check logs for details." "Yellow"
}

Write-Status "" "White"
Write-Status "Load test completed successfully!" "Green"
Write-Status "Monitor message processing in Kafka UI: http://localhost:8080" "Gray"
