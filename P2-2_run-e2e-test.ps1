# Run Comprehensive E2E Test - Simplified Test Runner
# This script runs the comprehensive end-to-end test and displays the results

Write-Host "========================================" -ForegroundColor Green
Write-Host "COMPREHENSIVE E2E TEST RUNNER" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "`nThis test will:" -ForegroundColor Cyan
Write-Host "• Verify all producer and consumer services are healthy" -ForegroundColor White
Write-Host "• Configure infinite retry for failed messages" -ForegroundColor White
Write-Host "• Send test messages to all registered consumer groups" -ForegroundColor White  
Write-Host "• Verify each consumer group receives and processes messages" -ForegroundColor White
Write-Host "• Monitor acknowledgments and retry failures automatically" -ForegroundColor White

Write-Host "`nTest Options:" -ForegroundColor Yellow
Write-Host "1. Quick Test (5 messages, with batching)" -ForegroundColor White
Write-Host "2. Standard Test (10 messages, with batching)" -ForegroundColor White
Write-Host "3. Non-Batching Test (5 messages, immediate processing)" -ForegroundColor White
Write-Host "4. Session-Specific Test (10 messages, counts only current session)" -ForegroundColor White
Write-Host "5. Custom Test (specify parameters)" -ForegroundColor White

$choice = Read-Host "`nSelect test type (1-5)"

switch ($choice) {
  '1' {
    Write-Host "`nRunning Quick Test..." -ForegroundColor Green
    & "$PSScriptRoot\e2e-comprehensive-test.ps1" -MessageCount 5 -UseBatching $true -VerificationTimeoutSeconds 120
  }
  '2' {
    Write-Host "`nRunning Standard Test..." -ForegroundColor Green
    & "$PSScriptRoot\e2e-comprehensive-test.ps1" -MessageCount 10 -UseBatching $true -VerificationTimeoutSeconds 180
  }
  '3' {
    Write-Host "`nRunning Non-Batching Test..." -ForegroundColor Green
    & "$PSScriptRoot\e2e-comprehensive-test.ps1" -MessageCount 5 -UseBatching $false -VerificationTimeoutSeconds 120
  }
  '4' {
    Write-Host "`nRunning Session-Specific Test..." -ForegroundColor Green
    & "$PSScriptRoot\e2e-session-specific-test.ps1" -MessageCount 10 -UseBatching $true -VerificationTimeoutSeconds 180
  }
  '5' {
    Write-Host "`nCustom Test Configuration:" -ForegroundColor Green
    $messageCount = Read-Host "Number of messages (default: 10)"
    if (-not $messageCount) { $messageCount = 10 }
        
    $useBatching = Read-Host "Use batching? (y/N)"
    $batchingFlag = $useBatching -eq 'y' -or $useBatching -eq 'Y'
        
    $timeout = Read-Host "Verification timeout in seconds (default: 180)"
    if (-not $timeout) { $timeout = 180 }
        
    Write-Host "`nRunning Custom Test..." -ForegroundColor Green
    & "$PSScriptRoot\e2e-comprehensive-test.ps1" -MessageCount $messageCount -UseBatching $batchingFlag -VerificationTimeoutSeconds $timeout
  }
  default {
    Write-Host "Invalid selection. Running session-specific test..." -ForegroundColor Yellow
    & "$PSScriptRoot\e2e-session-specific-test.ps1" -MessageCount 10 -UseBatching $true -VerificationTimeoutSeconds 180
  }
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "TEST COMPLETED" -ForegroundColor Green  
Write-Host "========================================" -ForegroundColor Green

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "• Monitor ongoing message processing: .\monitor-consumers.ps1" -ForegroundColor White
Write-Host "• Check message acknowledgments: .\verify-acknowledgments.ps1" -ForegroundColor White
Write-Host "• View Kafka messages: http://localhost:8080" -ForegroundColor White
Write-Host "• Check producer logs for retry attempts" -ForegroundColor White
