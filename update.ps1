$ErrorActionPreference = "Stop"

Write-Host "Updating Local LLM Server Manager..." -ForegroundColor Cyan

# 1. Pull latest code
Write-Host "Pulling latest changes from git..."
git pull

# 2. Stop the service if it's running
$ServiceName = "LocalLLMServerManager"
$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService -and $ExistingService.Status -eq 'Running') {
    Write-Host "Stopping service $ServiceName..."
    Stop-Service -Name $ServiceName -Force
}

# 3. Rebuild and publish
$InstallDir = Join-Path $env:SystemDrive "LocalLLMServerManager"
Write-Host "Rebuilding and publishing to $InstallDir..."
dotnet publish -c Release -o $InstallDir --nologo

# 4. Start the service
if ($ExistingService) {
    Write-Host "Starting service $ServiceName..."
    Start-Service -Name $ServiceName
}

Write-Host "Update Complete!" -ForegroundColor Green
