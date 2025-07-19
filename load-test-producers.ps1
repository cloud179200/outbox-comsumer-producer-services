# Load Test Script for Producer Services
# Sends 10 million messages concurrently to 3 producers in batches of 500

Write-Host "🚀 PRODUCER LOAD TEST - 10 MILLION MESSAGES" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

# Configuration
$TotalMessages = 10000000  # 10 million messages
$BatchSize = 500          # Messages per batch
$ProducerPorts = @(5301, 5302, 5303)  # Producer service ports
$Topic = "shared-events"
$MaxConcurrentJobs = 20   # Maximum concurrent PowerShell jobs
$ReportInterval = 100     # Report progress every N batches

# Calculate batches
$TotalBatches = [math]::Ceiling($TotalMessages / $BatchSize)
$BatchesPerProducer = [math]::Ceiling($TotalBatches / $ProducerPorts.Count)

Write-Host "📊 Test Configuration:" -ForegroundColor Cyan
Write-Host "  Total Messages: $($TotalMessages.ToString('N0'))" -ForegroundColor White
Write-Host "  Batch Size: $BatchSize" -ForegroundColor White
Write-Host "  Total Batches: $($TotalBatches.ToString('N0'))" -ForegroundColor White
Write-Host "  Batches per Producer: $($BatchesPerProducer.ToString('N0'))" -ForegroundColor White
Write-Host "  Max Concurrent Jobs: $MaxConcurrentJobs" -ForegroundColor White
Write-Host "  Producer Ports: $($ProducerPorts -join ', ')" -ForegroundColor White

# Check if services are running
Write-Host "`n🔍 Checking Producer Services..." -ForegroundColor Cyan
$healthyProducers = @()
foreach ($port in $ProducerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 10 -ErrorAction Stop
        Write-Host "  ✅ Producer on port $port is healthy" -ForegroundColor Green
        $healthyProducers += $port
    } catch {
        Write-Host "  ❌ Producer on port $port is not responding" -ForegroundColor Red
    }
}

if ($healthyProducers.Count -eq 0) {
    Write-Host "❌ No healthy producers found! Please start the Docker system first." -ForegroundColor Red
    Write-Host "Run: .\docker-manager.ps1 -Action Start" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Found $($healthyProducers.Count) healthy producers" -ForegroundColor Green

# Confirm before starting
Write-Host "`n⚠️  WARNING: This will send $($TotalMessages.ToString('N0')) messages to your system!" -ForegroundColor Yellow
Write-Host "This is a heavy load test that may impact system performance." -ForegroundColor Yellow
$confirm = Read-Host "Do you want to continue? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Load test cancelled." -ForegroundColor Yellow
    exit 0
}

# Initialize tracking variables
$script:CompletedBatches = 0
$script:CompletedMessages = 0
$script:FailedBatches = 0
$script:FailedMessages = 0
$script:StartTime = Get-Date
$script:LastReportTime = $script:StartTime

# Thread-safe increment functions
$script:LockObject = New-Object System.Object

function Update-Progress {
    param(
        [int]$BatchesCompleted,
        [int]$MessagesCompleted,
        [int]$BatchesFailed = 0,
        [int]$MessagesFailed = 0
    )
    
    [System.Threading.Monitor]::Enter($script:LockObject)
    try {
        $script:CompletedBatches += $BatchesCompleted
        $script:CompletedMessages += $MessagesCompleted
        $script:FailedBatches += $BatchesFailed
        $script:FailedMessages += $MessagesFailed
        
        if ($script:CompletedBatches % $ReportInterval -eq 0 -or $script:CompletedBatches -eq $TotalBatches) {
            $currentTime = Get-Date
            $elapsed = $currentTime - $script:StartTime
            $batchesPerSecond = if ($elapsed.TotalSeconds -gt 0) { $script:CompletedBatches / $elapsed.TotalSeconds } else { 0 }
            $messagesPerSecond = if ($elapsed.TotalSeconds -gt 0) { $script:CompletedMessages / $elapsed.TotalSeconds } else { 0 }
            $progressPercent = [math]::Round(($script:CompletedBatches / $TotalBatches) * 100, 2)
            
            Write-Host "📈 Progress: $($script:CompletedBatches.ToString('N0'))/$($TotalBatches.ToString('N0')) batches ($progressPercent%) | Messages: $($script:CompletedMessages.ToString('N0')) | Rate: $([math]::Round($messagesPerSecond, 0)) msg/s | Failed: $($script:FailedBatches)" -ForegroundColor Cyan
            
            $script:LastReportTime = $currentTime
        }
    }
    finally {
        [System.Threading.Monitor]::Exit($script:LockObject)
    }
}

# Function to send a batch of messages
$SendBatchFunction = {
    param(
        [int]$Port,
        [int]$BatchNumber,
        [int]$BatchSize,
        [string]$Topic,
        [int]$ProducerIndex
    )
    
    $successCount = 0
    $failCount = 0
    
    try {
        # Create batch payload
        $messages = @()
        for ($i = 1; $i -le $BatchSize; $i++) {
            $messageNumber = ($BatchNumber * $BatchSize) + $i
            $messages += @{
                topic = $Topic
                message = "Load test message #$messageNumber from Producer $ProducerIndex (batch $BatchNumber) - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')"
            }
        }
        
        # Send each message in the batch
        foreach ($msg in $messages) {
            try {
                $body = $msg | ConvertTo-Json
                $response = Invoke-RestMethod -Uri "http://localhost:$Port/api/messages/send" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 30
                if ($response.Status -eq "Queued") {
                    $successCount++
                } else {
                    $failCount++
                }
            } catch {
                $failCount++
            }
        }
        
        return @{
            Success = $true
            SuccessCount = $successCount
            FailCount = $failCount
            BatchNumber = $BatchNumber
            ProducerIndex = $ProducerIndex
        }
    }
    catch {
        return @{
            Success = $false
            SuccessCount = 0
            FailCount = $BatchSize
            BatchNumber = $BatchNumber
            ProducerIndex = $ProducerIndex
            Error = $_.Exception.Message
        }
    }
}

# Start the load test
Write-Host "`n🔥 Starting Load Test..." -ForegroundColor Green
Write-Host "Press Ctrl+C to stop the test early" -ForegroundColor Yellow

$jobs = @()
$batchNumber = 0

# Create jobs for each producer
for ($producerIndex = 0; $producerIndex -lt $healthyProducers.Count; $producerIndex++) {
    $port = $healthyProducers[$producerIndex]
    
    # Calculate batches for this producer
    $startBatch = $producerIndex * $BatchesPerProducer
    $endBatch = [math]::Min(($producerIndex + 1) * $BatchesPerProducer, $TotalBatches)
    
    Write-Host "📤 Producer $($producerIndex + 1) (port $port): batches $startBatch to $($endBatch - 1)" -ForegroundColor White
    
    # Create concurrent jobs for this producer
    for ($batch = $startBatch; $batch -lt $endBatch; $batch++) {
        # Wait if we have too many concurrent jobs
        while ((Get-Job -State Running).Count -ge $MaxConcurrentJobs) {
            Start-Sleep -Milliseconds 100
            
            # Check for completed jobs
            $completedJobs = Get-Job -State Completed
            foreach ($job in $completedJobs) {
                $result = Receive-Job -Job $job
                Remove-Job -Job $job
                
                if ($result.Success) {
                    Update-Progress -BatchesCompleted 1 -MessagesCompleted $result.SuccessCount -MessagesFailed $result.FailCount
                } else {
                    Update-Progress -BatchesFailed 1 -MessagesFailed $BatchSize
                    Write-Host "❌ Batch $($result.BatchNumber) failed on Producer $($result.ProducerIndex): $($result.Error)" -ForegroundColor Red
                }
            }
        }
        
        # Determine actual batch size (last batch might be smaller)
        $actualBatchSize = if ($batch -eq $TotalBatches - 1) { 
            $TotalMessages - ($batch * $BatchSize) 
        } else { 
            $BatchSize 
        }
        
        # Start job for this batch
        $job = Start-Job -ScriptBlock $SendBatchFunction -ArgumentList $port, $batch, $actualBatchSize, $Topic, ($producerIndex + 1)
        $jobs += $job
    }
}

# Wait for all jobs to complete
Write-Host "`n⏳ Waiting for all batches to complete..." -ForegroundColor Yellow

while ((Get-Job -State Running).Count -gt 0) {
    Start-Sleep -Seconds 1
    
    # Process completed jobs
    $completedJobs = Get-Job -State Completed
    foreach ($job in $completedJobs) {
        $result = Receive-Job -Job $job
        Remove-Job -Job $job
        
        if ($result.Success) {
            Update-Progress -BatchesCompleted 1 -MessagesCompleted $result.SuccessCount -MessagesFailed $result.FailCount
        } else {
            Update-Progress -BatchesFailed 1 -MessagesFailed $BatchSize
            Write-Host "❌ Batch $($result.BatchNumber) failed on Producer $($result.ProducerIndex): $($result.Error)" -ForegroundColor Red
        }
    }
}

# Clean up any remaining jobs
Get-Job | Remove-Job -Force

# Final statistics
$endTime = Get-Date
$totalDuration = $endTime - $script:StartTime
$averageMessagesPerSecond = if ($totalDuration.TotalSeconds -gt 0) { $script:CompletedMessages / $totalDuration.TotalSeconds } else { 0 }
$averageBatchesPerSecond = if ($totalDuration.TotalSeconds -gt 0) { $script:CompletedBatches / $totalDuration.TotalSeconds } else { 0 }

Write-Host "`n🎉 LOAD TEST COMPLETED!" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "📊 Final Statistics:" -ForegroundColor Cyan
Write-Host "  Total Duration: $($totalDuration.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor White
Write-Host "  Completed Batches: $($script:CompletedBatches.ToString('N0')) / $($TotalBatches.ToString('N0'))" -ForegroundColor White
Write-Host "  Completed Messages: $($script:CompletedMessages.ToString('N0')) / $($TotalMessages.ToString('N0'))" -ForegroundColor White
Write-Host "  Failed Batches: $($script:FailedBatches.ToString('N0'))" -ForegroundColor White
Write-Host "  Failed Messages: $($script:FailedMessages.ToString('N0'))" -ForegroundColor White
Write-Host "  Success Rate: $([math]::Round((($script:CompletedMessages - $script:FailedMessages) / $script:CompletedMessages) * 100, 2))%" -ForegroundColor White
Write-Host "  Average Messages/Second: $([math]::Round($averageMessagesPerSecond, 0))" -ForegroundColor White
Write-Host "  Average Batches/Second: $([math]::Round($averageBatchesPerSecond, 2))" -ForegroundColor White

# Performance analysis
Write-Host "`n📈 Performance Analysis:" -ForegroundColor Cyan
if ($averageMessagesPerSecond -gt 1000) {
    Write-Host "  🚀 Excellent performance! System handled high load efficiently." -ForegroundColor Green
} elseif ($averageMessagesPerSecond -gt 500) {
    Write-Host "  ✅ Good performance! System handled the load well." -ForegroundColor Green
} elseif ($averageMessagesPerSecond -gt 100) {
    Write-Host "  ⚠️  Moderate performance. Consider optimization for higher loads." -ForegroundColor Yellow
} else {
    Write-Host "  ❌ Low performance. System may be under stress or need optimization." -ForegroundColor Red
}

Write-Host "`n🔍 Recommendations:" -ForegroundColor Cyan
Write-Host "  • Monitor Docker container resources (CPU, Memory)" -ForegroundColor White
Write-Host "  • Check PostgreSQL performance and connections" -ForegroundColor White
Write-Host "  • Monitor Kafka broker performance" -ForegroundColor White
Write-Host "  • Use docker-compose logs -f to view service logs" -ForegroundColor White
Write-Host "  • Check Kafka UI (http://localhost:8080) for message flow" -ForegroundColor White

Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
