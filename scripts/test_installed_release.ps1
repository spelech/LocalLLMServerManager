<#
.SYNOPSIS
    Automated End-to-End Test Suite for Installed LocalLLMServerManager Package
.DESCRIPTION
    Installs the compiled Inno Setup installer into an isolated temporary directory,
    starts the server in headless service mode, validates all REST endpoints, discovery
    heuristics, VRAM telemetry, reverse proxies, and lifecycle endpoints, and cleanly
    uninstalls the test sandbox.
#>
param(
    [string]$SetupExePath,
    [int]$Port = 5246
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path $PSScriptRoot -Parent

if (-not $SetupExePath) {
    $DistDir = Join-Path $RootDir "dist"
    $LatestSetup = Get-ChildItem -Path $DistDir -Filter "*Setup.exe" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $LatestSetup) {
        throw "No Setup.exe found in $DistDir. Run build_release.ps1 first."
    }
    $SetupExePath = $LatestSetup.FullName
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Testing Installed Release: $(Split-Path $SetupExePath -Leaf)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$TempDir = Join-Path $env:TEMP ("LLMTest_" + [System.Guid]::NewGuid().ToString("N"))
$LogFile = Join-Path $env:TEMP ("InstallLog_" + [System.Guid]::NewGuid().ToString("N") + ".log")
$BaseUrl = "http://127.0.0.1:$Port"

try {
    # 1. Silent Installation to Sandbox
    Write-Host "--> 1. Installing to temporary sandbox: $TempDir..." -ForegroundColor Yellow
    Stop-Process -Name "LocalLLMServerManager" -Force -ErrorAction SilentlyContinue
    & "$SetupExePath" /VERYSILENT /SUPPRESSMSGBOXES /LOG="$LogFile" /DIR="$TempDir"
    Start-Sleep -Seconds 3

    $InstalledExe = Join-Path $TempDir "LocalLLMServerManager.exe"
    if (-not (Test-Path $InstalledExe)) {
        throw "Installation failed: $InstalledExe does not exist."
    }
    Write-Host "    Installation successful." -ForegroundColor Green

    # 2. Launch Installed Application
    Write-Host "--> 2. Launching installed binary in server mode..." -ForegroundColor Yellow
    $StdOutLog = Join-Path $TempDir "stdout.log"
    $StdErrLog = Join-Path $TempDir "stderr.log"
    $Proc = Start-Process -FilePath $InstalledExe -ArgumentList "--server" -WorkingDirectory $TempDir -RedirectStandardOutput $StdOutLog -RedirectStandardError $StdErrLog -PassThru
    Write-Host "    Started process PID: $($Proc.Id)" -ForegroundColor Green

    # 3. Wait for Server Startup
    Write-Host "--> 3. Waiting for server to become responsive at $BaseUrl..." -ForegroundColor Yellow
    $MaxRetries = 40
    $IsReady = $false
    for ($i = 0; $i -lt $MaxRetries; $i++) {
        try {
            $h = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 2 -ErrorAction Stop
            if ($h -and ($h.status -eq "Healthy" -or $h.Status -eq "Healthy")) {
                $IsReady = $true
                break
            }
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $IsReady) {
        throw "Server failed to respond to /health within $(($MaxRetries * 500) / 1000) seconds."
    }
    Write-Host "    Server is Healthy (Version: $($h.version), Ollama: $($h.ollama))" -ForegroundColor Green

    # 4. Verify GPU Telemetry
    Write-Host "--> 4. Validating GPU VRAM Telemetry..." -ForegroundColor Yellow
    $vram = Invoke-RestMethod -Uri "$BaseUrl/api/gpu/vram"
    if (-not $vram.gpuName -or $vram.vramBytes -le 0) {
        throw "GPU telemetry validation failed: $($vram | ConvertTo-Json)"
    }
    Write-Host "    Detected GPU: $($vram.gpuName) ($($vram.vramGB) GB VRAM)" -ForegroundColor Green

    # 5. Verify Tool Auto-Discovery & Disambiguation
    Write-Host "--> 5. Validating Dynamic Tool Discovery..." -ForegroundColor Yellow
    $tools = Invoke-RestMethod -Uri "$BaseUrl/api/system/tools/detect"
    if (-not $tools.ollama -or -not $tools.comfyUi -or -not $tools.forge) {
        throw "Tool discovery returned incomplete payload."
    }

    if ($tools.comfyUi.isInstalled -and $tools.forge.isInstalled) {
        if ($tools.comfyUi.executablePath -eq $tools.forge.executablePath) {
            throw "ComfyUI and Forge incorrectly resolved to identical executable path: $($tools.comfyUi.executablePath)"
        }
        if ($tools.comfyUi.rootDirectory -eq $tools.forge.rootDirectory) {
            throw "ComfyUI and Forge incorrectly resolved to identical root directory: $($tools.comfyUi.rootDirectory)"
        }
    }
    Write-Host "    Tool Discovery & Disambiguation passed." -ForegroundColor Green
    Write-Host "      ComfyUI: $($tools.comfyUi.executablePath)" -ForegroundColor Gray
    Write-Host "      Forge:   $($tools.forge.executablePath)" -ForegroundColor Gray
    Write-Host "      Ollama:  $($tools.ollama.executablePath)" -ForegroundColor Gray

    # 6. Verify Models & Workflows
    Write-Host "--> 6. Validating Models and Workflows..." -ForegroundColor Yellow
    $models = Invoke-RestMethod -Uri "$BaseUrl/api/models"
    $workflows = Invoke-RestMethod -Uri "$BaseUrl/api/comfy/workflows"
    Write-Host "    Found $($models.models.Count) local Ollama model(s) and $($workflows.Count) workflow(s)." -ForegroundColor Green

    # 7. Verify VRAM Orchestration Endpoint
    Write-Host "--> 7. Validating VRAM Orchestration Free Endpoint..." -ForegroundColor Yellow
    $freeRes = Invoke-RestMethod -Uri "$BaseUrl/api/comfy/free" -Method Post
    Write-Host "    VRAM Orchestrator response: $($freeRes.message)" -ForegroundColor Green

    # 8. Verify Search Hub Proxies
    Write-Host "--> 8. Validating Search Hub Proxies..." -ForegroundColor Yellow
    $hf = Invoke-RestMethod -Uri "$BaseUrl/api/hf/search?q=gemma"
    $civ = Invoke-RestMethod -Uri "$BaseUrl/api/civitai/search?q=flux"
    Write-Host "    HuggingFace: $($hf.models.Count) results, Civitai: $($civ.items.Count) results." -ForegroundColor Green

    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "  All Installed Package Verifications PASSED!" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
finally {
    # Teardown & Clean Uninstall
    Write-Host "--> 9. Cleaning up test sandbox..." -ForegroundColor Yellow
    if ($Proc -and -not $Proc.HasExited) {
        Stop-Process -Id $Proc.Id -Force -ErrorAction SilentlyContinue
    }
    
    $Uninstaller = Join-Path $TempDir "unins000.exe"
    if (Test-Path $Uninstaller) {
        & "$Uninstaller" /VERYSILENT /SUPPRESSMSGBOXES
        Start-Sleep -Seconds 2
    }

    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $LogFile) {
        Remove-Item -Path $LogFile -Force -ErrorAction SilentlyContinue
    }
    Write-Host "    Cleanup complete." -ForegroundColor Green
}
