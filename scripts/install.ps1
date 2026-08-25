# LocalLLMServerManager Installer Script

param(
    [string]$InstallDir = "",
    [switch]$InstallService,
    [switch]$Force,
    [switch]$WithVideo,
    [switch]$WithAudio,
    [switch]$Firewall,
    [switch]$NonInteractive
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
    if ($PSBoundParameters.ContainsKey('NonInteractive') -or $Force) {
        $InstallDir = $DefaultInstallDir
    } else {
        $UserInstallDir = Read-Host "Enter installation directory [Default: $DefaultInstallDir]"
        if ([string]::IsNullOrWhiteSpace($UserInstallDir)) {
            $InstallDir = $DefaultInstallDir
        } else {
            $InstallDir = $UserInstallDir
        }
    }
}

# Resolve and ensure directory exists
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Write-Host "Installing to: $InstallDir" -ForegroundColor Green

# 2. Service configuration decision
if (-not $PSBoundParameters.ContainsKey('InstallService') -and -not $PSBoundParameters.ContainsKey('NonInteractive') -and -not $Force) {
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

# 4. Check & Install Prerequisites (FFmpeg)
Write-Host "Checking FFmpeg prerequisite..." -ForegroundColor Cyan
$FFmpegCmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $FFmpegCmd) {
    Write-Warning "FFmpeg is not detected on system PATH."
    $InstallFFmpeg = $false
    if ($PSBoundParameters.ContainsKey('NonInteractive') -or $Force) {
        $InstallFFmpeg = $true
    } else {
        $FFmpegInput = Read-Host "Would you like to install FFmpeg via winget (official Gyan.FFmpeg package)? (Y/N) [Default: Y]"
        if ([string]::IsNullOrWhiteSpace($FFmpegInput) -or $FFmpegInput -eq "Y" -or $FFmpegInput -eq "y") {
            $InstallFFmpeg = $true
        }
    }
    if ($InstallFFmpeg) {
        Write-Host "Installing FFmpeg using WinGet..." -ForegroundColor Yellow
        try {
            winget install Gyan.FFmpeg --accept-package-agreements --accept-source-agreements --silent | Out-Null
            Write-Host "FFmpeg installed successfully!" -ForegroundColor Green
        } catch {
            Write-Warning "Winget installation of FFmpeg encountered an issue: $_"
        }
    }
} else {
    Write-Host "FFmpeg detected: $($FFmpegCmd.Source)" -ForegroundColor Green
}

# 5. Preserve existing settings.json so user configuration is never lost
$SettingsFile = Join-Path $InstallDir "settings.json"
$SettingsBackup = $null
if (Test-Path $SettingsFile) {
    Write-Host "Backing up existing settings.json..." -ForegroundColor Cyan
    $SettingsBackup = [System.IO.Path]::GetTempFileName()
    Copy-Item -Path $SettingsFile -Destination $SettingsBackup -Force
}

# 6. Build and Publish the application
Write-Host "Compiling and publishing application in Release mode..." -ForegroundColor Yellow
$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPath = Join-Path $ProjectDir "LocalLLMServerManager.csproj"
if (Test-Path $ProjectPath) {
    dotnet publish "$ProjectPath" -c Release -r win-x64 --self-contained false -o "$InstallDir" --nologo
} else {
    dotnet publish -c Release -r win-x64 --self-contained false -o "$InstallDir" --nologo
}

# 7. Restore preserved settings.json
if ($SettingsBackup -and (Test-Path $SettingsBackup)) {
    Write-Host "Restoring preserved settings.json..." -ForegroundColor Green
    Copy-Item -Path $SettingsBackup -Destination $SettingsFile -Force
    Remove-Item -Path $SettingsBackup -Force -ErrorAction SilentlyContinue
}

$ExePath = Join-Path $InstallDir "LocalLLMServerManager.exe"

# 8. Configure / Restart Windows Service
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

# 9. Configure Windows Defender Firewall Rule for Port 5246
$ConfigureFirewall = $Firewall
if (-not $PSBoundParameters.ContainsKey('Firewall') -and -not $PSBoundParameters.ContainsKey('NonInteractive') -and -not $Force) {
    $FirewallInput = Read-Host "Configure Windows Defender Firewall rule for port 5246 (allow LAN & MCP router access)? (Y/N) [Default: Y]"
    if ([string]::IsNullOrWhiteSpace($FirewallInput) -or $FirewallInput -eq "Y" -or $FirewallInput -eq "y") {
        $ConfigureFirewall = $true
    }
}

if ($ConfigureFirewall) {
    if (Test-Administrator) {
        Write-Host "Configuring Windows Defender Firewall inbound rule on TCP port 5246..." -ForegroundColor Yellow
        netsh.exe advfirewall firewall delete rule name="LocalLLM Server Manager" | Out-Null
        netsh.exe advfirewall firewall add rule name="LocalLLM Server Manager" dir=in action=allow protocol=TCP localport=5246 | Out-Null
        Write-Host "Firewall rule created for port 5246." -ForegroundColor Green
    } else {
        Write-Warning "Administrator privileges required to add firewall rules. Skipping firewall configuration."
    }
}

# 10. Configure System Tray Auto-Start on User Logon
Write-Host "Configuring System Tray App to auto-start on logon..." -ForegroundColor Yellow
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
                 -Name "LocalLLMServerManagerTray" `
                 -Value "`"$ExePath`"" -ErrorAction SilentlyContinue
Write-Host "System Tray auto-start configured!" -ForegroundColor Green

# 11. Feature Pack Optional Installation
if ($WithVideo) {
    Write-Host "Installing Video Generation Feature Pack..." -ForegroundColor Cyan
    $videoWorkflowDir = Join-Path $InstallDir "Workflows\Video"
    if (-not (Test-Path $videoWorkflowDir)) {
        New-Item -ItemType Directory -Path $videoWorkflowDir -Force | Out-Null
    }
}

if ($WithAudio) {
    Write-Host "Installing Audio & Kokoro TTS Feature Pack..." -ForegroundColor Cyan
    $kokoroDir = Join-Path $InstallDir "kokoro-fastapi"
    $audioModelsDir = Join-Path $InstallDir "models\audio"
    if (-not (Test-Path $kokoroDir)) { New-Item -ItemType Directory -Path $kokoroDir -Force | Out-Null }
    if (-not (Test-Path $audioModelsDir)) { New-Item -ItemType Directory -Path $audioModelsDir -Force | Out-Null }

    $PyCmd = Get-Command python -ErrorAction SilentlyContinue
    if (-not $PyCmd) {
        $PyCmd = Get-Command python3 -ErrorAction SilentlyContinue
    }
    if ($PyCmd) {
        Write-Host "Installing/verifying Python audio packages (kokoro-onnx, soundfile, fastapi, uvicorn, openai)..." -ForegroundColor Yellow
        try {
            & $PyCmd.Source -m pip install kokoro-onnx soundfile fastapi uvicorn openai --quiet
            Write-Host "Python audio packages installed successfully." -ForegroundColor Green
        } catch {
            Write-Warning "Failed to install Python audio packages: $_"
        }
    } else {
        Write-Warning "Python not found on PATH. Please install Python 3.10+ for Kokoro TTS audio support."
    }
}

# 12. Relaunch Tray Application if it was running
if ($HadRunningProcesses) {
    Write-Host "Relaunching LocalLLMServerManager tray application..." -ForegroundColor Yellow
    Start-Process -FilePath $ExePath -ErrorAction SilentlyContinue
}

# 13. Detect Primary LAN IP address for summary
$LanIp = "10.0.0.21"
try {
    $DetectedIp = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.InterfaceAlias -notmatch "vEthernet|Loopback|WSL" -and $_.IPAddress -notmatch "^127\.|^169\.254\." } | Select-Object -First 1).IPAddress
    if (-not [string]::IsNullOrWhiteSpace($DetectedIp)) {
        $LanIp = $DetectedIp
    }
} catch { }

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "           Installation Complete!         " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Local Dashboard:    http://localhost:5246" -ForegroundColor Green
Write-Host "Local MCP Endpoint: http://localhost:5246/mcp" -ForegroundColor Green
Write-Host "Network Dashboard:  http://${LanIp}:5246" -ForegroundColor Cyan
Write-Host "Network MCP:        http://${LanIp}:5246/mcp" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
