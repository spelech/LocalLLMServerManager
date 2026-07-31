# LocalLLMServerManager - Tray Application Auto-Start Setup
$ErrorActionPreference = "Stop"

$RegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$Name = "LocalLLMServerManagerTray"
$ExePath = Join-Path $env:SystemDrive "LocalLLMServerManager\LocalLLMServerManager.exe"

if (-not (Test-Path $ExePath)) {
    $ExePath = Join-Path $PSScriptRoot "bin\Release\net10.0\LocalLLMServerManager.exe"
}

Write-Host "Registering LocalLLMServerManager System Tray App for User Logon..."
Set-ItemProperty -Path $RegistryPath -Name $Name -Value "`"$ExePath`""

Write-Host "System Tray App configured to start automatically on user login!" -ForegroundColor Green
