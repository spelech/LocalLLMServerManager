param(
    [string]$InstallDir = $(if (Test-Path "C:\Program Files\LocalLLMServerManager") { "C:\Program Files\LocalLLMServerManager" } else { Join-Path $env:SystemDrive "LocalLLMServerManager" }),
    [switch]$SkipGitPull
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "    Local LLM Server Manager Updater      " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Pull latest code from git (if not skipped)
if (-not $SkipGitPull) {
    Write-Host "Pulling latest changes from git..." -ForegroundColor Cyan
    git pull
}

# 2. Stop the Windows Service if running
$ServiceName = "LocalLLMServerManager"
$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$ServiceWasRunning = $false

if ($ExistingService -and $ExistingService.Status -eq 'Running') {
    Write-Host "Stopping service $ServiceName..." -ForegroundColor Yellow
    $ServiceWasRunning = $true
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# 3. Detect and kill running tray UI processes to release file locks
$RunningProcesses = Get-Process -Name "LocalLLMServerManager" -ErrorAction SilentlyContinue
$HadRunningProcesses = $false
if ($RunningProcesses) {
    Write-Host "Stopping running LocalLLMServerManager processes to release file locks..." -ForegroundColor Yellow
    $HadRunningProcesses = $true
    $RunningProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# 4. Preserve existing settings.json so user configuration is never overwritten
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
$SettingsFile = Join-Path $InstallDir "settings.json"
$SettingsBackup = $null

if (Test-Path $SettingsFile) {
    Write-Host "Backing up existing settings.json..." -ForegroundColor Cyan
    $SettingsBackup = [System.IO.Path]::GetTempFileName()
    Copy-Item -Path $SettingsFile -Destination $SettingsBackup -Force
}

# 5. Rebuild and publish
Write-Host "Rebuilding and publishing to $InstallDir..." -ForegroundColor Yellow
$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPath = Join-Path $ProjectDir "LocalLLMServerManager.csproj"
if (Test-Path $ProjectPath) {
    dotnet publish "$ProjectPath" -c Release -r win-x64 --self-contained -o "$InstallDir" --nologo /p:PublishSingleFile=false
} else {
    dotnet publish -c Release -r win-x64 --self-contained -o "$InstallDir" --nologo /p:PublishSingleFile=false
}

# 6. Restore preserved settings.json
if ($SettingsBackup -and (Test-Path $SettingsBackup)) {
    Write-Host "Restoring preserved settings.json..." -ForegroundColor Green
    Copy-Item -Path $SettingsBackup -Destination $SettingsFile -Force
    Remove-Item -Path $SettingsBackup -Force -ErrorAction SilentlyContinue
}

# 7. Restart service if it was previously running or registered
if ($ExistingService -or $ServiceWasRunning) {
    Write-Host "Starting service $ServiceName..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
}

# 8. Relaunch tray application if it was running before update
if ($HadRunningProcesses) {
    $ExePath = Join-Path $InstallDir "LocalLLMServerManager.exe"
    if (Test-Path $ExePath) {
        Write-Host "Relaunching LocalLLMServerManager tray application..." -ForegroundColor Yellow
        Start-Process -FilePath $ExePath -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Update Complete!" -ForegroundColor Green
Write-Host "The dashboard is available at http://localhost:5246" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
