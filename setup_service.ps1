# Requires Run as Administrator
$ErrorActionPreference = "Stop"

$ServiceName = "LocalLLMServerManager"
$ExePath = Join-Path $env:SystemDrive "LocalLLMServerManager\LocalLLMServerManager.exe"

$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService) {
    Write-Host "Service already exists. Restarting..."
    Restart-Service -Name $ServiceName
} else {
    Write-Host "Registering Windows Service to start on boot..."
    New-Service -Name $ServiceName `
                -BinaryPathName "`"$ExePath`" --service" `
                -DisplayName "Local LLM Server Manager" `
                -Description "Orchestrates GPU VRAM between Ollama and Forge, and manages local model weights." `
                -StartupType Automatic | Out-Null
    
    Start-Service -Name $ServiceName
    Write-Host "Service installed and started!"
}
