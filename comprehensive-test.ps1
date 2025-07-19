# Comprehensive System Test and Fix Script
# This script tests all components and fixes any issues found

Write-Host "COMPREHENSIVE SYSTEM TEST AND DIAGNOSTICS" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

# Test Configuration
$ProducerPorts = @(5301, 5302, 5303)
$ConsumerPorts = @(5401, 5402, 5403, 5404, 5405, 5406)
$TestTopic = "shared-events"

# Results tracking
$TestResults = @{
    ProducersHealthy = 0
    ConsumersHealthy = 0
    TopicsRegistered = 0
    MessagesSuccessful = 0
    TotalTests = 0
    Issues = @()
}

# Test 1: Docker Container Status
Write-Host "`n1. DOCKER CONTAINER STATUS" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan

try {
    $containers = docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    Write-Host $containers -ForegroundColor White
    
    $runningContainers = docker ps --format "{{.Names}}" | Where-Object { $_ -match "outbox-" }
    Write-Host "Running containers: $($runningContainers.Count)" -ForegroundColor Yellow
} catch {
    Write-Host "ERROR: Cannot check Docker containers" -ForegroundColor Red
    $TestResults.Issues += "Docker containers check failed"
}

# Test 2: Producer Health Checks
Write-Host "`n2. PRODUCER HEALTH CHECKS" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan

foreach ($port in $ProducerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/health" -Method GET -TimeoutSec 5
        Write-Host "Producer $port : $($response.Status)" -ForegroundColor Green
        $TestResults.ProducersHealthy++
    } catch {
        Write-Host "Producer $port : FAILED - $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.Issues += "Producer $port health check failed"
    }
    $TestResults.TotalTests++
}

# Test 3: Consumer Health Checks
Write-Host "`n3. CONSUMER HEALTH CHECKS" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan

foreach ($port in $ConsumerPorts) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/consumer/health" -Method GET -TimeoutSec 5
        Write-Host "Consumer $port : $($response.Status)" -ForegroundColor Green
        $TestResults.ConsumersHealthy++
    } catch {
        Write-Host "Consumer $port : FAILED - $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.Issues += "Consumer $port health check failed"
    }
    $TestResults.TotalTests++
}

# Test 4: Topic Registration Check
Write-Host "`n4. TOPIC REGISTRATION CHECK" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

foreach ($port in $ProducerPorts) {
    try {
        $topics = Invoke-RestMethod -Uri "http://localhost:$port/api/topics" -Method GET -TimeoutSec 5
        $hasSharedEvents = $topics | Where-Object { $_.topicName -eq $TestTopic }
        
        if ($hasSharedEvents) {
            Write-Host "Producer $port : Has '$TestTopic' topic" -ForegroundColor Green
            $TestResults.TopicsRegistered++
        } else {
            Write-Host "Producer $port : Missing '$TestTopic' topic - FIXING..." -ForegroundColor Yellow
            
            # Register the topic
            $topicRegistration = @{
                TopicName = $TestTopic
                Description = "Shared events topic for scaling demo"
                ConsumerGroups = @(
                    @{
                        ConsumerGroupName = "group-a"
                        RequiresAcknowledgment = $true
                        AcknowledgmentTimeoutMinutes = 30
                        MaxRetries = 3
                    },
                    @{
                        ConsumerGroupName = "group-b"
                        RequiresAcknowledgment = $true
                        AcknowledgmentTimeoutMinutes = 30
                        MaxRetries = 3
                    },
                    @{
                        ConsumerGroupName = "group-c"
                        RequiresAcknowledgment = $true
                        AcknowledgmentTimeoutMinutes = 30
                        MaxRetries = 3
                    }
                )
            } | ConvertTo-Json -Depth 3

            try {
                $response = Invoke-RestMethod -Uri "http://localhost:$port/api/topics/register" -Method POST -Body $topicRegistration -ContentType "application/json" -TimeoutSec 10
                Write-Host "Producer $port : Topic registered successfully" -ForegroundColor Green
                $TestResults.TopicsRegistered++
            } catch {
                if ($_.Exception.Message -match "409") {
                    Write-Host "Producer $port : Topic already exists" -ForegroundColor Green
                    $TestResults.TopicsRegistered++
                } else {
                    Write-Host "Producer $port : Topic registration failed - $($_.Exception.Message)" -ForegroundColor Red
                    $TestResults.Issues += "Producer $port topic registration failed"
                }
            }
        }
    } catch {
        Write-Host "Producer $port : Cannot check topics - $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.Issues += "Producer $port topics check failed"
    }
    $TestResults.TotalTests++
}

# Test 5: Message Sending Test
Write-Host "`n5. MESSAGE SENDING TEST" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan

foreach ($port in $ProducerPorts) {
    try {
        $body = @{
            Topic = $TestTopic
            Message = "Test message from producer $port at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/messages/send" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 10
        Write-Host "Producer $port : Message sent successfully (ID: $($response.messageId))" -ForegroundColor Green
        $TestResults.MessagesSuccessful++
    } catch {
        Write-Host "Producer $port : Message sending failed - $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.Issues += "Producer $port message sending failed"
    }
    $TestResults.TotalTests++
}

# Test 6: Database Connectivity
Write-Host "`n6. DATABASE CONNECTIVITY" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan

try {
    $pgTest = docker exec outbox-postgres psql -U outbox_user -d outbox_db -c "SELECT 1;" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "PostgreSQL : Connection successful" -ForegroundColor Green
    } else {
        Write-Host "PostgreSQL : Connection failed" -ForegroundColor Red
        $TestResults.Issues += "PostgreSQL connection failed"
    }
} catch {
    Write-Host "PostgreSQL : Connection test failed - $($_.Exception.Message)" -ForegroundColor Red
    $TestResults.Issues += "PostgreSQL connection test failed"
}

# Test 7: Kafka Connectivity
Write-Host "`n7. KAFKA CONNECTIVITY" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan

try {
    $kafkaTest = docker exec outbox-kafka kafka-topics --bootstrap-server localhost:9092 --list 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Kafka : Connection successful" -ForegroundColor Green
    } else {
        Write-Host "Kafka : Connection failed" -ForegroundColor Red
        $TestResults.Issues += "Kafka connection failed"
    }
} catch {
    Write-Host "Kafka : Connection test failed - $($_.Exception.Message)" -ForegroundColor Red
    $TestResults.Issues += "Kafka connection test failed"
}

# Final Results
Write-Host "`n8. FINAL RESULTS" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan

Write-Host "Test Summary:" -ForegroundColor Yellow
Write-Host "  Producers Healthy: $($TestResults.ProducersHealthy)/3" -ForegroundColor White
Write-Host "  Consumers Healthy: $($TestResults.ConsumersHealthy)/6" -ForegroundColor White
Write-Host "  Topics Registered: $($TestResults.TopicsRegistered)/3" -ForegroundColor White
Write-Host "  Messages Successful: $($TestResults.MessagesSuccessful)/3" -ForegroundColor White
Write-Host "  Total Tests: $($TestResults.TotalTests)" -ForegroundColor White

if ($TestResults.Issues.Count -eq 0) {
    Write-Host "`nSYSTEM STATUS: ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "The system is fully operational and ready for load testing." -ForegroundColor Green
} else {
    Write-Host "`nSYSTEM STATUS: ISSUES DETECTED" -ForegroundColor Yellow
    Write-Host "Issues found:" -ForegroundColor Red
    foreach ($issue in $TestResults.Issues) {
        Write-Host "  - $issue" -ForegroundColor Red
    }
}

# Calculate overall health score
$totalPossible = 3 + 6 + 3 + 3  # 3 producers + 6 consumers + 3 topics + 3 messages
$actualSuccess = $TestResults.ProducersHealthy + $TestResults.ConsumersHealthy + $TestResults.TopicsRegistered + $TestResults.MessagesSuccessful
$healthScore = [math]::Round(($actualSuccess / $totalPossible) * 100, 1)

Write-Host "`nOVERALL HEALTH SCORE: $healthScore%" -ForegroundColor $(if ($healthScore -ge 90) { "Green" } elseif ($healthScore -ge 70) { "Yellow" } else { "Red" })

if ($healthScore -ge 90) {
    Write-Host "EXCELLENT: Ready for production load testing!" -ForegroundColor Green
} elseif ($healthScore -ge 70) {
    Write-Host "GOOD: System is mostly functional with minor issues" -ForegroundColor Yellow
} else {
    Write-Host "NEEDS ATTENTION: Major issues detected" -ForegroundColor Red
}

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Run load test: .\load-test-10m.ps1" -ForegroundColor White
Write-Host "  2. Monitor Kafka: http://localhost:8080" -ForegroundColor White
Write-Host "  3. Monitor Database: http://localhost:8082" -ForegroundColor White
Write-Host "  4. Check system logs: docker-compose logs -f" -ForegroundColor White
