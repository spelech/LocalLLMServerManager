# LocalLLMServerManager Installer Script

param(
    [string]$InstallDir = "",
    [switch]$InstallService,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Check for Admin privileges
function Test-Administrator {
    $user = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $user.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "      Local LLM Server Manager Installer    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Determine installation directory
$DefaultInstallDir = Join-Path $env:SystemDrive "LocalLLMServerManager"
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $UserInstallDir = Read-Host "Enter installation directory [Default: $DefaultInstallDir]"
    if ([string]::IsNullOrWhiteSpace($UserInstallDir)) {
        $InstallDir = $DefaultInstallDir
    } else {
        $InstallDir = $UserInstallDir
    }
}

# Resolve and ensure directory exists
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Write-Host "Installing to: $InstallDir" -ForegroundColor Green

# 2. Service configuration decision
if (-not $PSBoundParameters.ContainsKey('InstallService')) {
    $InstallServiceInput = Read-Host "Do you want to install as a background Windows Service? (Y/N) [Default: N]"
    if ($InstallServiceInput -eq "Y" -or $InstallServiceInput -eq "y") {
        $InstallService = $true
    }
}

if ($InstallService -and -not (Test-Administrator)) {
    Write-Warning "Administrator privileges are required to install Windows Services."
    Write-Warning "Please restart this PowerShell session as Administrator and run the installer again."
    exit 1
}

# 3. Detect and gracefully stop running Windows Service and tray processes before file updates
$ServiceName = "LocalLLMServerManager"
$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$ServiceWasRunning = $false

if ($ExistingService -and $ExistingService.Status -eq 'Running') {
    Write-Host "Stopping running Windows Service ($ServiceName)..." -ForegroundColor Yellow
    $ServiceWasRunning = $true
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

$RunningProcesses = Get-Process -Name "LocalLLMServerManager" -ErrorAction SilentlyContinue
$HadRunningProcesses = $false
if ($RunningProcesses) {
    Write-Host "Stopping running LocalLLMServerManager processes to release file locks..." -ForegroundColor Yellow
    $HadRunningProcesses = $true
    $RunningProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# 4. Preserve existing settings.json so user configuration is never lost
$SettingsFile = Join-Path $InstallDir "settings.json"
$SettingsBackup = $null
if (Test-Path $SettingsFile) {
    Write-Host "Backing up existing settings.json..." -ForegroundColor Cyan
    $SettingsBackup = [System.IO.Path]::GetTempFileName()
    Copy-Item -Path $SettingsFile -Destination $SettingsBackup -Force
}

# 5. Build and Publish the application
Write-Host "Compiling and publishing application in Release mode..." -ForegroundColor Yellow
$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPath = Join-Path $ProjectDir "LocalLLMServerManager.csproj"
if (Test-Path $ProjectPath) {
    dotnet publish "$ProjectPath" -c Release -o "$InstallDir" --nologo
} else {
    dotnet publish -c Release -o "$InstallDir" --nologo
}

# 6. Restore preserved settings.json
if ($SettingsBackup -and (Test-Path $SettingsBackup)) {
    Write-Host "Restoring preserved settings.json..." -ForegroundColor Green
    Copy-Item -Path $SettingsBackup -Destination $SettingsFile -Force
    Remove-Item -Path $SettingsBackup -Force -ErrorAction SilentlyContinue
}

$ExePath = Join-Path $InstallDir "LocalLLMServerManager.exe"

# 7. Configure / Restart Windows Service
if ($InstallService) {
    $ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($ExistingService) {
        Write-Host "Reconfiguring existing Windows Service..." -ForegroundColor Yellow
        sc.exe config $ServiceName binPath= "`"$ExePath`" --service" start= auto displayName= "Local LLM Server Manager" | Out-Null
        sc.exe description $ServiceName "Orchestrates GPU VRAM between Ollama and Forge, and manages local model weights." | Out-Null
    } else {
        Write-Host "Registering Windows Service..." -ForegroundColor Yellow
        New-Service -Name $ServiceName `
                    -BinaryPathName "`"$ExePath`" --service" `
                    -DisplayName "Local LLM Server Manager" `
                    -Description "Orchestrates GPU VRAM between Ollama and Forge, and manages local model weights." `
                    -StartupType Automatic | Out-Null
    }
    
    Write-Host "Starting Windows Service..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName
    Write-Host "Service installed and started successfully!" -ForegroundColor Green
} elseif ($ServiceWasRunning) {
    Write-Host "Restarting previously running Windows Service ($ServiceName)..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
} else {
    Write-Host "Skipped Windows Service installation." -ForegroundColor Yellow
    Write-Host "You can run the app manually by executing:" -ForegroundColor Cyan
    Write-Host "  $InstallDir\LocalLLMServerManager.exe" -ForegroundColor Cyan
}

# 8. Configure System Tray Auto-Start on User Logon
Write-Host "Configuring System Tray App to auto-start on logon..." -ForegroundColor Yellow
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
                 -Name "LocalLLMServerManagerTray" `
                 -Value "`"$ExePath`"" -ErrorAction SilentlyContinue
Write-Host "System Tray auto-start configured!" -ForegroundColor Green

# 9. Relaunch Tray Application if it was running
if ($HadRunningProcesses) {
    Write-Host "Relaunching LocalLLMServerManager tray application..." -ForegroundColor Yellow
    Start-Process -FilePath $ExePath -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Installation Complete!" -ForegroundColor Green
Write-Host "The dashboard is available at http://localhost:5246" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
