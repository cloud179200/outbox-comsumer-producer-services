# Comprehensive Message Acknowledgment Verification Script
Write-Host "MESSAGE ACKNOWLEDGMENT VERIFICATION" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

# Configuration
$ProducerPorts = @(5301, 5302, 5303)
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$ConsumerGroups = @("group-a", "group-b", "group-c")

Write-Host "`nChecking Message Processing Status Across All Consumer Groups..." -ForegroundColor Cyan

foreach ($group in $ConsumerGroups) {
    Write-Host "`nConsumer Group: $group" -ForegroundColor Yellow
    Write-Host "=" * 50 -ForegroundColor Yellow
    
    # Get processed messages from each consumer
    $totalProcessed = 0
    $totalFailed = 0
    
    foreach ($port in $ConsumerPorts) {
        try {
            # Check processed messages
            $processedUrl = "http://localhost:$port/api/consumer/processed/$group"
            $processedMessages = Invoke-RestMethod -Uri $processedUrl -Method GET -TimeoutSec 10
            
            if ($processedMessages -and $processedMessages.Count -gt 0) {
                Write-Host "  Consumer $port : $($processedMessages.Count) processed messages" -ForegroundColor Green
                $totalProcessed += $processedMessages.Count
                
                # Show latest processed messages
                $latest = $processedMessages | Select-Object -First 3
                foreach ($msg in $latest) {
                    Write-Host "    ✅ $($msg.MessageId) at $($msg.ProcessedAt)" -ForegroundColor White
                }
            }
            
            # Check failed messages
            $failedUrl = "http://localhost:$port/api/consumer/failed/$group"
            $failedMessages = Invoke-RestMethod -Uri $failedUrl -Method GET -TimeoutSec 10
            
            if ($failedMessages -and $failedMessages.Count -gt 0) {
                Write-Host "  Consumer $port : $($failedMessages.Count) failed messages" -ForegroundColor Red
                $totalFailed += $failedMessages.Count
                
                # Show latest failed messages
                $latest = $failedMessages | Select-Object -First 3
                foreach ($msg in $latest) {
                    Write-Host "    ❌ $($msg.MessageId) - $($msg.ErrorMessage)" -ForegroundColor Red
                }
            }
            
        } catch {
            Write-Host "  Consumer $port : Not responding or error" -ForegroundColor Gray
        }
    }
    
    Write-Host "  Group Total: $totalProcessed processed, $totalFailed failed" -ForegroundColor Cyan
}

Write-Host "`nChecking Producer Outbox Status..." -ForegroundColor Cyan

foreach ($port in $ProducerPorts) {
    try {
        Write-Host "`nProducer $port" -ForegroundColor Yellow
        
        # Check pending messages
        $pendingUrl = "http://localhost:$port/api/messages/pending"
        $pendingMessages = Invoke-RestMethod -Uri $pendingUrl -Method GET -TimeoutSec 10
        
        if ($pendingMessages) {
            Write-Host "  Pending: $($pendingMessages.Count) messages" -ForegroundColor Yellow
        }
        
        # Check messages for each consumer group
        foreach ($group in $ConsumerGroups) {
            $groupUrl = "http://localhost:$port/api/messages/consumer-group/$group"
            $groupMessages = Invoke-RestMethod -Uri $groupUrl -Method GET -TimeoutSec 10
            
            if ($groupMessages) {
                $statusCounts = $groupMessages | Group-Object Status | ForEach-Object { "$($_.Name): $($_.Count)" }
                Write-Host "  $group : $($groupMessages.Count) total [$($statusCounts -join ', ')]" -ForegroundColor White
            }
        }
        
    } catch {
        Write-Host "  Producer $port : Not responding" -ForegroundColor Red
    }
}

Write-Host "`nMessage Status Legend:" -ForegroundColor Cyan
Write-Host "• Pending: Message created, waiting to be sent" -ForegroundColor Yellow
Write-Host "• Sent: Message sent to Kafka" -ForegroundColor Blue  
Write-Host "• Acknowledged: Consumer confirmed successful processing" -ForegroundColor Green
Write-Host "• Failed: Consumer reported processing failure" -ForegroundColor Red
Write-Host "• Expired: Message exceeded retry timeout" -ForegroundColor DarkRed

Write-Host "`nAcknowledgment verification completed!" -ForegroundColor Green
Write-Host "Check the counts above to see if consumers are acknowledging messages properly." -ForegroundColor White
