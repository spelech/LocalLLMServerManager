# LocalLLMServerManager Fast Update Script
# Updates installed application in < 15 seconds without full Inno Setup repackaging

param(
    [string]$InstallDir = "",
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path $PSScriptRoot -Parent

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   LocalLLMServerManager Fast Hot-Update   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Detect installed directory
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $RunningProc = Get-Process -Name "LocalLLMServerManager*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($RunningProc -and $RunningProc.Path) {
        $InstallDir = Split-Path $RunningProc.Path -Parent
    } elseif (Test-Path "C:\Program Files\LocalLLMServerManager") {
        $InstallDir = "C:\Program Files\LocalLLMServerManager"
    } elseif (Test-Path "C:\LocalLLMServerManager") {
        $InstallDir = "C:\LocalLLMServerManager"
    } else {
        $InstallDir = "C:\Program Files\LocalLLMServerManager"
    }
}

Write-Host "Target Installation: $InstallDir" -ForegroundColor Green

# 2. Stop running processes / service to release locks
$ServiceName = "LocalLLMServerManager"
$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$ServiceWasRunning = $false
if ($ExistingService -and $ExistingService.Status -eq 'Running') {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    $ServiceWasRunning = $true
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
}

$RunningProcs = Get-Process -Name "LocalLLMServerManager*" -ErrorAction SilentlyContinue
if ($RunningProcs) {
    Write-Host "Stopping running processes..." -ForegroundColor Yellow
    $RunningProcs | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
}

# 3. Fast compile WebAssembly UI
Write-Host "Compiling WebAssembly UI..." -ForegroundColor Cyan
dotnet publish "$RootDir\LocalLLMServerManager.Web\LocalLLMServerManager.Web.csproj" -c Release -r browser-wasm --nologo
$AppBundleFramework = Join-Path "$RootDir\LocalLLMServerManager.Web\bin\Release\net10.0\browser-wasm\AppBundle" "_framework"
$RepoFramework = Join-Path "$RootDir\wwwroot" "_framework"
if (Test-Path $AppBundleFramework) {
    Copy-Item -Path "$AppBundleFramework\*" -Destination "$RepoFramework" -Recurse -Force
}

# 4. Fast compile & publish backend to installation dir
Write-Host "Compiling and publishing backend binaries..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

dotnet publish "$RootDir\LocalLLMServerManager.csproj" -c Release -r win-x64 --self-contained -o "$InstallDir" --nologo /p:PublishSingleFile=false

# Copy wwwroot assets to installed folder
$RepoWwwroot = Join-Path $RootDir "wwwroot"
$InstallWwwroot = Join-Path $InstallDir "wwwroot"
if (Test-Path $RepoWwwroot) {
    Copy-Item -Path "$RepoWwwroot\*" -Destination "$InstallWwwroot\" -Recurse -Force
}

# 5. Restart service or launch process
$ExePath = Join-Path $InstallDir "LocalLLMServerManager.exe"
if ($ServiceWasRunning) {
    Write-Host "Restarting Windows Service..." -ForegroundColor Green
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
} elseif (-not $NoLaunch -and (Test-Path $ExePath)) {
    Write-Host "Launching updated application..." -ForegroundColor Green
    Start-Process -FilePath $ExePath
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "   Fast Update Complete in < 15 seconds!  " -ForegroundColor Green
Write-Host "   Dashboard: http://localhost:5246       " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Green
