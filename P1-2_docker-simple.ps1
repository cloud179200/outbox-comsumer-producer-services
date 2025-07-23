# Docker Scaled System Management Script
Write-Host "Docker Scaled Outbox System Management" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

function Show-Status {
    Write-Host "`nCurrent System Status:" -ForegroundColor Cyan
    
    # Check if Docker is running
    try {
        docker version | Out-Null
        Write-Host "Docker is running" -ForegroundColor Green
    } catch {
        Write-Host "Docker is not running or not accessible" -ForegroundColor Red
        return
    }
    
    # Check running containers
    Write-Host "`nRunning Containers:" -ForegroundColor Cyan
    docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
}

function Start-InfrastructureOnly {
    Write-Host "`nStarting Infrastructure Only (PostgreSQL, Kafka, Zookeeper)..." -ForegroundColor Cyan
    docker-compose up -d postgres zookeeper kafka kafka-ui pgadmin
    
    Write-Host "`nWaiting for infrastructure to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    
    Show-Status
}

function Build-Services {
    Write-Host "`nBuilding Services..." -ForegroundColor Cyan
    
    Write-Host "Building Producer Service..." -ForegroundColor White
    docker-compose build producer1
    
    Write-Host "Building Consumer Service..." -ForegroundColor White
    docker-compose build consumer1
    
    Write-Host "Services built successfully!" -ForegroundColor Green
}

function Start-AllServices {
    Write-Host "`nStarting All Services..." -ForegroundColor Cyan
    docker-compose up -d
    
    Write-Host "`nWaiting for services to start..." -ForegroundColor Yellow
    Start-Sleep -Seconds 45
    
    Show-Status
}

function Stop-AllServices {
    Write-Host "`nStopping All Services..." -ForegroundColor Cyan
    docker-compose down
    
    Write-Host "All services stopped!" -ForegroundColor Green
}

function Test-Services {
    Write-Host "`nTesting Services..." -ForegroundColor Cyan
    
    # Test Producer Services
    Write-Host "`nTesting Producer Services:" -ForegroundColor White
    for ($i = 1; $i -le 3; $i++) {
        $port = 5300 + $i
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 10
            Write-Host "  Producer $i (port $port) is healthy" -ForegroundColor Green
        } catch {
            Write-Host "  Producer $i (port $port) is not responding" -ForegroundColor Red
        }
    }
    
    # Test Consumer Services
    Write-Host "`nTesting Consumer Services:" -ForegroundColor White
    for ($i = 1; $i -le 6; $i++) {
        $port = 5400 + $i
        $group = switch ($i) {
            {$_ -in 1..3} { "Group A" }
            {$_ -in 4..5} { "Group B" }
            6 { "Group C" }
        }
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 10
            Write-Host "  Consumer $i (port $port, $group) is healthy" -ForegroundColor Green
        } catch {
            Write-Host "  Consumer $i (port $port, $group) is not responding" -ForegroundColor Red
        }
    }
}

function Send-TestMessages {
    Write-Host "`nSending Test Messages..." -ForegroundColor Cyan
    
    for ($i = 1; $i -le 3; $i++) {
        $port = 5300 + $i
        $message = "Test message from Producer $i via Docker at $(Get-Date -Format 'HH:mm:ss')"
        
        try {
            $body = @{
                topic = "shared-events"
                message = $message
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/send" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 15
            Write-Host "  Message sent via Producer $i" -ForegroundColor Green
        } catch {
            Write-Host "  Failed to send message via Producer $i - $($_.Exception.Message)" -ForegroundColor Red
        }
        Start-Sleep -Seconds 1
    }
}

function Show-Logs {
    param([string]$ServiceName)
    
    if ($ServiceName) {
        Write-Host "`nShowing logs for $ServiceName..." -ForegroundColor Cyan
        docker-compose logs -f $ServiceName
    } else {
        Write-Host "`nShowing logs for all services..." -ForegroundColor Cyan
        docker-compose logs -f
    }
}

function Show-Menu {
    Write-Host "`nAvailable Actions:" -ForegroundColor Cyan
    Write-Host "  1. Show Status" -ForegroundColor White
    Write-Host "  2. Start Infrastructure Only" -ForegroundColor White
    Write-Host "  3. Build Services" -ForegroundColor White
    Write-Host "  4. Start All Services" -ForegroundColor White
    Write-Host "  5. Test Services" -ForegroundColor White
    Write-Host "  6. Send Test Messages" -ForegroundColor White
    Write-Host "  7. Show Logs (All)" -ForegroundColor White
    Write-Host "  8. Show Logs (Specific Service)" -ForegroundColor White
    Write-Host "  9. Stop All Services" -ForegroundColor White
    Write-Host "  0. Exit" -ForegroundColor White
}

# Main execution
Show-Status

while ($true) {
    Show-Menu
    $choice = Read-Host "`nEnter your choice (0-9)"
    
    switch ($choice) {
        "1" { Show-Status }
        "2" { Start-InfrastructureOnly }
        "3" { Build-Services }
        "4" { Start-AllServices }
        "5" { Test-Services }
        "6" { Send-TestMessages }
        "7" { Show-Logs }
        "8" { 
            $serviceName = Read-Host "Enter service name (e.g., producer1, consumer1, kafka)"
            Show-Logs -ServiceName $serviceName
        }
        "9" { Stop-AllServices }
        "0" { 
            Write-Host "`nGoodbye!" -ForegroundColor Green
            break 
        }
        default { Write-Host "`nInvalid choice. Please try again." -ForegroundColor Red }
    }
}
