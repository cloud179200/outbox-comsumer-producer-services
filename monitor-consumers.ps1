# Consumer Monitoring Script for Load Test
# Monitors consumer services during load testing

Write-Host "🔍 CONSUMER MONITORING DURING LOAD TEST" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

# Configuration
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$ConsumerGroups = @{
    5401 = "group-a"; 5402 = "group-a"; 5403 = "group-a"
    5404 = "group-b"; 5405 = "group-b"; 5406 = "group-c"
}
$MonitoringInterval = 5  # seconds
$ReportInterval = 60     # seconds for detailed report

# Check if consumers are running
Write-Host "🔍 Checking Consumer Services..." -ForegroundColor Cyan
$healthyConsumers = @()
foreach ($port in $ConsumerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 10 -ErrorAction Stop
        Write-Host "  ✅ Consumer on port $port ($($ConsumerGroups[$port])) is healthy" -ForegroundColor Green
        $healthyConsumers += @{ Port = $port; Group = $ConsumerGroups[$port] }
    } catch {
        Write-Host "  ❌ Consumer on port $port is not responding" -ForegroundColor Red
    }
}

if ($healthyConsumers.Count -eq 0) {
    Write-Host "❌ No healthy consumers found! Please start the Docker system first." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Found $($healthyConsumers.Count) healthy consumers" -ForegroundColor Green

# Initialize monitoring
$startTime = Get-Date
$lastReportTime = $startTime
$previousStats = @{}

Write-Host "`n🚀 Starting Consumer Monitoring..." -ForegroundColor Green
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Write-Host "Legend: Group A (3 consumers), Group B (2 consumers), Group C (1 consumer)" -ForegroundColor Gray

try {
    while ($true) {
        $currentTime = Get-Date
        $totalElapsed = $currentTime - $startTime
        
        # Collect stats from all consumers
        $consumerStats = @()
        $groupStats = @{}
        
        foreach ($consumer in $healthyConsumers) {
            try {
                # Get consumer health/stats
                $healthResponse = Invoke-RestMethod -Uri "http://localhost:$($consumer.Port)/api/consumer/health" -Method GET -TimeoutSec 5
                
                # Try to get processed message count (if endpoint exists)
                $processedCount = 0
                try {
                    $processedResponse = Invoke-RestMethod -Uri "http://localhost:$($consumer.Port)/api/consumer/processed-count" -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
                    $processedCount = $processedResponse.Count
                } catch {
                    # Endpoint might not exist, use 0
                }
                
                $stats = @{
                    Port = $consumer.Port
                    Group = $consumer.Group
                    Status = $healthResponse.Status
                    ProcessedCount = $processedCount
                    Timestamp = $currentTime
                }
                
                $consumerStats += $stats
                
                # Group statistics
                if (-not $groupStats.ContainsKey($consumer.Group)) {
                    $groupStats[$consumer.Group] = @{
                        Count = 0
                        ProcessedTotal = 0
                        HealthyCount = 0
                    }
                }
                
                $groupStats[$consumer.Group].Count++
                $groupStats[$consumer.Group].ProcessedTotal += $processedCount
                if ($healthResponse.Status -eq "Healthy") {
                    $groupStats[$consumer.Group].HealthyCount++
                }
                
            } catch {
                $stats = @{
                    Port = $consumer.Port
                    Group = $consumer.Group
                    Status = "Error"
                    ProcessedCount = 0
                    Timestamp = $currentTime
                }
                $consumerStats += $stats
            }
        }
        
        # Display current status
        Write-Host "`n⏰ $($currentTime.ToString('HH:mm:ss')) - Elapsed: $($totalElapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Cyan
        
        # Display by group
        foreach ($group in @("group-a", "group-b", "group-c")) {
            if ($groupStats.ContainsKey($group)) {
                $groupData = $groupStats[$group]
                $groupConsumers = $consumerStats | Where-Object { $_.Group -eq $group }
                
                Write-Host "📦 $group ($($groupData.HealthyCount)/$($groupData.Count) healthy) - Total processed: $($groupData.ProcessedTotal.ToString('N0'))" -ForegroundColor White
                
                foreach ($consumer in $groupConsumers) {
                    $statusColor = if ($consumer.Status -eq "Healthy") { "Green" } else { "Red" }
                    $processingRate = 0
                    
                    # Calculate processing rate if we have previous data
                    if ($previousStats.ContainsKey($consumer.Port)) {
                        $timeDiff = ($currentTime - $previousStats[$consumer.Port].Timestamp).TotalSeconds
                        if ($timeDiff -gt 0) {
                            $countDiff = $consumer.ProcessedCount - $previousStats[$consumer.Port].ProcessedCount
                            $processingRate = [math]::Round($countDiff / $timeDiff, 1)
                        }
                    }
                    
                    Write-Host "    Port $($consumer.Port): $($consumer.Status) | Processed: $($consumer.ProcessedCount.ToString('N0')) | Rate: $processingRate/sec" -ForegroundColor $statusColor
                }
            }
        }
        
        # Store current stats for next iteration
        $previousStats = @{}
        foreach ($consumer in $consumerStats) {
            $previousStats[$consumer.Port] = $consumer
        }
        
        # Detailed report every minute
        if (($currentTime - $lastReportTime).TotalSeconds -ge $ReportInterval) {
            Write-Host "`n📊 DETAILED REPORT - $($currentTime.ToString('HH:mm:ss'))" -ForegroundColor Yellow
            Write-Host "===========================================" -ForegroundColor Yellow
            
            $totalProcessed = ($consumerStats | Measure-Object -Property ProcessedCount -Sum).Sum
            $totalHealthy = ($consumerStats | Where-Object { $_.Status -eq "Healthy" }).Count
            $overallRate = if ($totalElapsed.TotalSeconds -gt 0) { $totalProcessed / $totalElapsed.TotalSeconds } else { 0 }
            
            Write-Host "  Total Consumers: $($consumerStats.Count)" -ForegroundColor White
            Write-Host "  Healthy Consumers: $totalHealthy" -ForegroundColor White
            Write-Host "  Total Messages Processed: $($totalProcessed.ToString('N0'))" -ForegroundColor White
            Write-Host "  Overall Processing Rate: $([math]::Round($overallRate, 1)) messages/sec" -ForegroundColor White
            
            # Group breakdown
            Write-Host "  Group Breakdown:" -ForegroundColor White
            foreach ($group in @("group-a", "group-b", "group-c")) {
                if ($groupStats.ContainsKey($group)) {
                    $groupData = $groupStats[$group]
                    Write-Host "    $group: $($groupData.ProcessedTotal.ToString('N0')) messages ($($groupData.HealthyCount)/$($groupData.Count) healthy)" -ForegroundColor Gray
                }
            }
            
            $lastReportTime = $currentTime
        }
        
        Start-Sleep -Seconds $MonitoringInterval
    }
} catch {
    Write-Host "`n⚠️  Monitoring stopped: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n🏁 Consumer monitoring ended." -ForegroundColor Green
