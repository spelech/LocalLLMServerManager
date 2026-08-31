# LocalLLMServerManager v3.11.0 — Release Build & Package Script
$ErrorActionPreference = "Stop"

$Version = "3.11.0"
$RootDir = Split-Path $PSScriptRoot -Parent
$PublishDir = Join-Path $RootDir "publish"
$DistDir = Join-Path $RootDir "dist"
$ZipFile = Join-Path $DistDir "LocalLLMServerManager-v$Version-win-x64.zip"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Building Release Package v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Run Verification & Test Suite
Write-Host "--> 1. Running Test Suite & Verification..." -ForegroundColor Yellow
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ToolDiscoveryServiceTests|FullyQualifiedName~DiscoveryEndpointsTests" --nologo -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed!" }

# 2. Publish Avalonia WebAssembly to wwwroot & Build Self-Contained Release
Write-Host "--> 2. Building & Publishing Avalonia WebAssembly (Wasm) UI..." -ForegroundColor Yellow
dotnet publish LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj -c Release -r browser-wasm --nologo
if ($LASTEXITCODE -ne 0) { throw "Avalonia Wasm publish failed!" }

# Overwrite wwwroot\_framework with compiled Wasm launcher and assets
$AppBundleFramework = Join-Path "$RootDir\LocalLLMServerManager.Web\bin\Release\net10.0\browser-wasm\AppBundle" "_framework"
$FrameworkDir = Join-Path "$RootDir\wwwroot" "_framework"
if (Test-Path $FrameworkDir) { Remove-Item -Path $FrameworkDir -Recurse -Force }
New-Item -ItemType Directory -Path $FrameworkDir -Force | Out-Null

Copy-Item -Path "$AppBundleFramework\*" -Destination $FrameworkDir -Recurse -Force
# Remove any precompressed .br or .gz files
Get-ChildItem -Path $FrameworkDir -Include *.br, *.gz -Recurse | Remove-Item -Force
Get-ChildItem -Path "$RootDir\wwwroot" -Include *.br, *.gz -Recurse | Remove-Item -Force

Write-Host "--> 3. Publishing Self-Contained win-x64 Executable..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Path $PublishDir -Recurse -Force }
if (Test-Path $DistDir) { Remove-Item -Path $DistDir -Recurse -Force }
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

dotnet publish LocalLLMServerManager.csproj -c Release -r win-x64 --self-contained -o $PublishDir --nologo /p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed!" }

# 3. Create Release Zip Archive
Write-Host "--> 4. Creating Release Zip Archive..." -ForegroundColor Yellow
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipFile -Force
Write-Host "Zip Archive created: $ZipFile" -ForegroundColor Green

# 4. Compile Inno Setup Installer if ISCC.exe is available
$IsccPath = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Path
if (-not $IsccPath) {
    $PossiblePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $PossiblePaths) {
        if (Test-Path $p) {
            $IsccPath = $p
            break
        }
    }
}

if ($IsccPath) {
    Write-Host "--> 5. Compiling Windows Installer (Inno Setup)..." -ForegroundColor Yellow
    & "$IsccPath" (Join-Path $RootDir "scripts\installer.iss")
    $SetupExe = Join-Path $DistDir "LocalLLMServerManager-v$Version-Setup.exe"
    if (Test-Path $SetupExe) {
        Write-Host "Installer created: $SetupExe" -ForegroundColor Green
    }
} else {
    Write-Host "--> 5. ISCC.exe (Inno Setup) not found. Skipped installer creation." -ForegroundColor Yellow
    Write-Host "     (Install Inno Setup 6 to enable automated installer generation locally)." -ForegroundColor Gray
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Release Build Complete!" -ForegroundColor Green
Write-Host "  Outputs located in: $DistDir" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

