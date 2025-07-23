# Cleanup Script for Outbox Pattern Demo
# This script stops all services and cleans up resources

Write-Host "🧹 Cleaning up Outbox Pattern Demo Environment" -ForegroundColor Yellow
Write-Host "===============================================" -ForegroundColor Yellow

# Stop Docker containers
Write-Host "`n📦 Stopping Docker containers..." -ForegroundColor Cyan
try {
  docker-compose down -v
  Write-Host "✅ Docker containers stopped and volumes removed" -ForegroundColor Green
}
catch {
  Write-Host "⚠️  Error stopping containers (they may not be running)" -ForegroundColor Yellow
}

# Optional: Remove Docker images
$removeImages = Read-Host "`n🗑️  Do you want to remove Docker images? (y/N)"
if ($removeImages -eq 'y' -or $removeImages -eq 'Y') {
  Write-Host "🗑️  Removing Docker images..." -ForegroundColor Cyan
    
  $images = @(
    "postgres:16-alpine",
    "confluentinc/cp-zookeeper:7.4.0", 
    "confluentinc/cp-kafka:7.4.0"
  )
    
  foreach ($image in $images) {
    try {
      docker rmi $image -f 2>$null
      Write-Host "✅ Removed image: $image" -ForegroundColor Green
    }
    catch {
      Write-Host "⚠️  Could not remove image: $image (may not exist)" -ForegroundColor Yellow
    }
  }
}

# Clean build artifacts
Write-Host "`n🧽 Cleaning build artifacts..." -ForegroundColor Cyan

$foldersToClean = @(
  "ProducerService\bin",
  "ProducerService\obj", 
  "ConsumerService\bin",
  "ConsumerService\obj"
)

foreach ($folder in $foldersToClean) {
  if (Test-Path $folder) {
    try {
      Remove-Item $folder -Recurse -Force
      Write-Host "✅ Cleaned: $folder" -ForegroundColor Green
    }
    catch {
      Write-Host "⚠️  Could not clean: $folder" -ForegroundColor Yellow
    }
  }
}

# Optional: Clear NuGet cache
$clearNuget = Read-Host "`n📦 Do you want to clear NuGet cache? (y/N)"
if ($clearNuget -eq 'y' -or $clearNuget -eq 'Y') {
  Write-Host "📦 Clearing NuGet cache..." -ForegroundColor Cyan
  try {
    dotnet nuget locals all --clear
    Write-Host "✅ NuGet cache cleared" -ForegroundColor Green
  }
  catch {
    Write-Host "⚠️  Error clearing NuGet cache" -ForegroundColor Yellow
  }
}

Write-Host "`n✅ Cleanup completed!" -ForegroundColor Green
Write-Host "═══════════════════" -ForegroundColor Green

Write-Host "`n📝 Manual cleanup (if needed):" -ForegroundColor Cyan
Write-Host "  • Close any open service console windows" -ForegroundColor White
Write-Host "  • Check Task Manager for any remaining dotnet processes" -ForegroundColor White
Write-Host "  • Restart Docker Desktop if containers won't stop" -ForegroundColor White

Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
