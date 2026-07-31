# LocalLLMServerManager v2.0.0 — Release Build & Package Script
$ErrorActionPreference = "Stop"

$Version = "2.0.0"
$RootDir = $PSScriptRoot
$PublishDir = Join-Path $RootDir "publish"
$DistDir = Join-Path $RootDir "dist"
$ZipFile = Join-Path $DistDir "LocalLLMServerManager-v$Version-win-x64.zip"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Building Release Package v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Run Verification & Test Suite
Write-Host "--> 1. Running Test Suite & Verification..." -ForegroundColor Yellow
dotnet test --nologo -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed!" }

Write-Host "--> 2. Running Frontend Linting & Type Checks..." -ForegroundColor Yellow
npm run lint
if ($LASTEXITCODE -ne 0) { throw "npm run lint failed!" }

npx tsc --noEmit
if ($LASTEXITCODE -ne 0) { throw "npx tsc --noEmit failed!" }

# 2. Clean & Publish Self-Contained Release
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
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
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
    & "$IsccPath" (Join-Path $RootDir "installer.iss")
    $SetupExe = Join-Path $RootDir "LocalLLMServerManager-v$Version-Setup.exe"
    if (Test-Path $SetupExe) {
        Move-Item -Path $SetupExe -Destination $DistDir -Force
        Write-Host "Installer created: $DistDir\LocalLLMServerManager-v$Version-Setup.exe" -ForegroundColor Green
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
