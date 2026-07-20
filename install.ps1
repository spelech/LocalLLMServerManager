# LocalLLMServerManager Installer Script

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

# 1. Ask for installation directory
$DefaultInstallDir = Join-Path $env:SystemDrive "LocalLLMServerManager"
$InstallDir = Read-Host "Enter installation directory [Default: $DefaultInstallDir]"
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = $DefaultInstallDir
}

# Resolve and ensure directory exists
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Write-Host "Installing to: $InstallDir" -ForegroundColor Green

# 2. Ask if they want to install as a Windows Service
$InstallServiceInput = Read-Host "Do you want to install as a background Windows Service? (Y/N) [Default: N]"
$InstallService = $false
if ($InstallServiceInput -eq "Y" -or $InstallServiceInput -eq "y") {
    $InstallService = $true
}

if ($InstallService -and -not (Test-Administrator)) {
    Write-Warning "Administrator privileges are required to install Windows Services."
    Write-Warning "Please restart this PowerShell session as Administrator and run the installer again."
    exit 1
}

# 3. Build and Publish the application
Write-Host "Compiling and publishing application in Release mode..." -ForegroundColor Yellow
dotnet publish -c Release -o $InstallDir --nologo

# 4. Configure Windows Service if requested
if ($InstallService) {
    $ServiceName = "LocalLLMServerManager"
    $ExePath = Join-Path $InstallDir "LocalLLMServerManager.exe"
    
    # Check if service already exists
    $ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($ExistingService) {
        Write-Host "Existing service found. Stopping and removing..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
        # Use sc.exe delete to ensure it is fully removed
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }
    
    Write-Host "Registering Windows Service..." -ForegroundColor Yellow
    New-Service -Name $ServiceName `
                -BinaryPathName "`"$ExePath`"" `
                -DisplayName "Local LLM Server Manager" `
                -Description "Orchestrates GPU VRAM between Ollama and Forge, and manages local model weights." `
                -StartupType Automatic | Out-Null
                
    Write-Host "Starting Windows Service..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName
    
    Write-Host "Service installed and started successfully!" -ForegroundColor Green
} else {
    Write-Host "Skipped Windows Service installation." -ForegroundColor Yellow
    Write-Host "You can run the app manually by executing:" -ForegroundColor Cyan
    Write-Host "  $InstallDir\LocalLLMServerManager.exe" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Installation Complete!" -ForegroundColor Green
Write-Host "The dashboard is available at http://localhost:5246" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
