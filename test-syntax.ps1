# Test script to validate E2E script syntax
param(
    [string]$ProducerUrl = "http://localhost:5301"
)

# Test basic variable assignment
Write-Host "Testing basic functionality..." -ForegroundColor Green

# Test function call
function Test-Function {
    param([string]$url)
    Write-Host "Function called with: $url" -ForegroundColor Yellow
    return $true
}

$result = Test-Function -url $ProducerUrl
Write-Host "Function result: $result" -ForegroundColor Green

# Test hashtable creation
$testHash = @{
    MessageId = "test-123"
    Message = "test message"
    Status = "sent"
}

Write-Host "Hashtable created: $($testHash.MessageId)" -ForegroundColor Green
Write-Host "Syntax test completed successfully!" -ForegroundColor Green
