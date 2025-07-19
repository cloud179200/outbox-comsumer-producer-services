# Simple Load Testing Script for Docker Scaled Outbox System
# Sends messages concurrently to 3 producer services in configurable batches

param(
    [int]$TotalMessages = 10000,     # Default: 10,000 messages (for testing)
    [int]$BatchSize = 100,           # Default: 100 messages per batch
    [int]$MaxConcurrency = 20,       # Maximum concurrent requests
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
    param([string]$Message, [string]$Color = "Cyan")
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
    
    # Create runspace pool for concurrent execution
    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $MaxConcurrency)
    $runspacePool.Open()
    
    $jobs = @()
    
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
        
        # Create PowerShell instance
        $powershell = [powershell]::Create()
        $powershell.RunspacePool = $runspacePool
        
        # Add script to send message
        $null = $powershell.AddScript({
            param($Url, $Body)
            try {
                $response = Invoke-RestMethod -Uri $Url -Method POST -Body $Body -ContentType "application/json" -TimeoutSec 10
                return @{ Success = $true; MessageId = $response.MessageId }
            } catch {
                return @{ Success = $false; Error = $_.Exception.Message }
            }
        }).AddArgument($Producer.Url).AddArgument($requestBody)
        
        # Start the job
        $job = @{
            PowerShell = $powershell
            Handle = $powershell.BeginInvoke()
            MessageIndex = $i
        }
        $jobs += $job
    }
    
    # Wait for all jobs to complete
    foreach ($job in $jobs) {
        try {
            $result = $job.PowerShell.EndInvoke($job.Handle)
            if ($result.Success) {
                $sent++
                if ($Verbose) {
                    Write-Status "✅ Message $($job.MessageIndex) sent via $($Producer.Name)" "Green"
                }
            } else {
                $failed++
                if ($Verbose) {
                    Write-Status "❌ Message $($job.MessageIndex) failed via $($Producer.Name): $($result.Error)" "Red"
                }
            }
        } catch {
            $failed++
            if ($Verbose) {
                Write-Status "❌ Message $($job.MessageIndex) failed via $($Producer.Name): $($_.Exception.Message)" "Red"
            }
        } finally {
            $job.PowerShell.Dispose()
        }
    }
    
    $runspacePool.Close()
    $runspacePool.Dispose()
    
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
Write-Status "🚀 Starting Load Test for Docker Scaled Outbox System" "Green"
Write-Status "====================================================" "Green"

Write-Status "📋 Configuration:" "Cyan"
Write-Status "  • Total Messages: $($TotalMessages.ToString('N0'))" "White"
Write-Status "  • Batch Size: $BatchSize" "White"
Write-Status "  • Max Concurrency: $MaxConcurrency" "White"
Write-Status "  • Target Topic: $Topic" "White"
Write-Status "  • Producers: $($Producers.Count)" "White"

# Health check
Write-Status "`n🩺 Checking producer health..." "Cyan"
foreach ($producer in $Producers) {
    try {
        $healthUrl = $producer.Url.Replace("/api/messages/send", "/api/messages/health")
        $response = Invoke-RestMethod -Uri $healthUrl -Method GET -TimeoutSec 5
        if ($response.Status -eq "Healthy") {
            Write-Status "  ✅ $($producer.Name) is healthy" "Green"
        } else {
            Write-Status "  ⚠️ $($producer.Name) status: $($response.Status)" "Yellow"
        }
    } catch {
        Write-Status "  ❌ $($producer.Name) is not responding" "Red"
    }
}

# Calculate batches
$totalBatches = [math]::Ceiling($TotalMessages / $BatchSize)
$Stats.StartTime = Get-Date

Write-Status "`n🔄 Starting load test..." "Cyan"
Write-Status "  • Total batches: $totalBatches" "White"

# Process batches across producers
$batchResults = @()
$batchNumber = 0

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

Write-Status "`n🎉 Load test completed!" "Green"
Write-Status "======================" "Green"

# Final statistics
Write-Status "`n📊 Final Statistics:" "Cyan"
Write-Status "  • Total Messages: $($TotalMessages.ToString('N0'))" "White"
Write-Status "  • Messages Sent: $($Stats.TotalSent.ToString('N0'))" "Green"
Write-Status "  • Messages Failed: $($Stats.TotalFailed.ToString('N0'))" "Red"
Write-Status "  • Success Rate: $([math]::Round(($Stats.TotalSent / $TotalMessages) * 100, 2))%" "Green"
Write-Status "  • Total Duration: $($totalDuration.ToString('mm\:ss\.fff'))" "White"
Write-Status "  • Average Throughput: $([math]::Round($Stats.TotalSent / $totalDuration.TotalSeconds, 2)) msg/sec" "Cyan"

Write-Status "`n🏭 Producer Statistics:" "Cyan"
foreach ($producerName in $Stats.Producers.Keys) {
    $producerStats = $Stats.Producers[$producerName]
    $total = $producerStats.Sent + $producerStats.Failed
    $successRate = if ($total -gt 0) { [math]::Round(($producerStats.Sent / $total) * 100, 2) } else { 0 }
    Write-Status "  • $producerName - Sent: $($producerStats.Sent.ToString('N0')), Failed: $($producerStats.Failed.ToString('N0')), Success Rate: $successRate%" "White"
}

Write-Status "`n⚡ Performance Summary:" "Cyan"
$avgBatchDuration = ($batchResults | Measure-Object -Property Duration -Average).Average
$avgBatchThroughput = ($batchResults | Measure-Object -Property Throughput -Average).Average
Write-Status "  • Average Batch Duration: $([math]::Round($avgBatchDuration, 2)) ms" "White"
Write-Status "  • Average Batch Throughput: $([math]::Round($avgBatchThroughput, 2)) msg/sec" "White"
Write-Status "  • Total Batches: $totalBatches" "White"

if ($Stats.TotalFailed -gt 0) {
    Write-Status "`n⚠️ Warning: $($Stats.TotalFailed) messages failed. Check logs for details." "Yellow"
}

Write-Status "`n✅ Load test completed successfully!" "Green"
Write-Status "Monitor message processing in Kafka UI: http://localhost:8080" "Gray"
