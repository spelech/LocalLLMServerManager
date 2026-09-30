<#
.SYNOPSIS
    Automated Version Bumping, Release Packaging & Deployment Script for LocalLLMServerManager.

.DESCRIPTION
    Bumps version across all project files (.csproj, .axaml, .js, .iss, docs, and test assertions),
    runs verification (lint, typecheck, tests), commits, tags, and can trigger remote GitHub release
    or compile the local Inno Setup installer.

.PARAMETER Bump
    Type of semver bump: "patch", "minor", or "major". Defaults to "minor" if neither is specified.

.PARAMETER Version
    Explicit version override (e.g. "3.18.0"). Overrides -Bump if specified.

.PARAMETER Commit
    Creates a git commit with the version bump. Defaults to $true.

.PARAMETER Tag
    Creates an annotated git tag "v<Version>". Defaults to $true.

.PARAMETER Push
    Pushes commit and tags to origin/main to trigger the GitHub Actions release workflow. Defaults to $false.

.PARAMETER BuildInstaller
    Runs scripts/build_release.ps1 locally to produce win-x64 zip and Inno Setup installer. Defaults to $false.

.PARAMETER DryRun
    Previews the changes without writing to disk or executing git actions.
#>

[CmdletBinding()]
param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "minor",

    [string]$Version = "",

    [switch]$NoCommit,
    [switch]$NoTag,
    [switch]$Push,
    [switch]$BuildInstaller,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path $PSScriptRoot -Parent
$MainCsproj = Join-Path $RootDir "LocalLLMServerManager.csproj"

# 1. Read Current Version from LocalLLMServerManager.csproj
if (-not (Test-Path $MainCsproj)) {
    throw "Cannot find LocalLLMServerManager.csproj at $MainCsproj"
}

$csprojContent = Get-Content $MainCsproj -Raw
if ($csprojContent -match '<Version>(\d+\.\d+\.\d+)</Version>') {
    $CurrentVersion = $Matches[1]
} else {
    throw "Could not extract current <Version> from $MainCsproj"
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  LocalLLMServerManager Release & Versioning" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Current Version: $CurrentVersion" -ForegroundColor Yellow

# 2. Compute Target Version
if ($Version) {
    $TargetVersion = $Version.TrimStart('v')
} else {
    $parts = $CurrentVersion.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]

    switch ($Bump) {
        "major" { $major += 1; $minor = 0; $patch = 0 }
        "minor" { $minor += 1; $patch = 0 }
        "patch" { $patch += 1 }
    }
    $TargetVersion = "$major.$minor.$patch"
}

$TargetAssemblyVersion = "$TargetVersion.0"
Write-Host "Target Version:  $TargetVersion (Assembly: $TargetAssemblyVersion)" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "[DryRun] Would bump from $CurrentVersion to $TargetVersion across all project files." -ForegroundColor Magenta
    return
}

# 3. Helper to replace in file
function Replace-InFile {
    param(
        [string]$FilePath,
        [string]$Pattern,
        [string]$Replacement
    )
    $fullPath = Join-Path $RootDir $FilePath
    if (Test-Path $fullPath) {
        $content = Get-Content $fullPath -Raw
        if ($content -match $Pattern) {
            $newContent = [regex]::Replace($content, $Pattern, $Replacement)
            Set-Content -Path $fullPath -Value $newContent -NoNewline
            Write-Host "  [OK] Updated $FilePath" -ForegroundColor Gray
        } else {
            Write-Host "  [SKIP] Pattern not found in $FilePath" -ForegroundColor DarkGray
        }
    } else {
        Write-Host "  [WARN] File not found: $FilePath" -ForegroundColor Yellow
    }
}

Write-Host "--> 1. Updating Version Identifiers Across Solution..." -ForegroundColor Yellow

# 1. Main Project .csproj
Replace-InFile "LocalLLMServerManager.csproj" "<Version>.*?</Version>" "<Version>$TargetVersion</Version>"

# 2. Shared Project .csproj
Replace-InFile "LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj" "<Version>.*?</Version>" "<Version>$TargetVersion</Version>"
Replace-InFile "LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj" "<AssemblyVersion>.*?</AssemblyVersion>" "<AssemblyVersion>$TargetAssemblyVersion</AssemblyVersion>"
Replace-InFile "LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj" "<FileVersion>.*?</FileVersion>" "<FileVersion>$TargetAssemblyVersion</FileVersion>"

# 3. Web Wasm Project .csproj
Replace-InFile "LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj" "<Version>.*?</Version>" "<Version>$TargetVersion</Version>"
Replace-InFile "LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj" "<AssemblyVersion>.*?</AssemblyVersion>" "<AssemblyVersion>$TargetAssemblyVersion</AssemblyVersion>"
Replace-InFile "LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj" "<FileVersion>.*?</FileVersion>" "<FileVersion>$TargetAssemblyVersion</FileVersion>"

# 4. JavaScript Client entrypoints
Replace-InFile "LocalLLMServerManager.Web/main.js" 'const APP_VERSION = ".*?";' "const APP_VERSION = `"$TargetVersion`";"
Replace-InFile "wwwroot/main.js" 'const APP_VERSION = ".*?";' "const APP_VERSION = `"$TargetVersion`";"
Replace-InFile "wwwroot/index.html" 'src="\./main\.js\?v=.*?"' "src=`"./main.js?v=$TargetVersion`""

# 5. C# API Health Endpoints
Replace-InFile "Endpoints/HealthEndpoints.cs" 'Version = ".*?",' "Version = `"$TargetVersion`","

# 6. Inno Setup & Build scripts
Replace-InFile "scripts/installer.iss" '#define MyAppVersion ".*?"' "#define MyAppVersion `"$TargetVersion`""
Replace-InFile "scripts/installer.iss" 'LocalLLMServerManager v\d+\.\d+\.\d+' "LocalLLMServerManager v$TargetVersion"
Replace-InFile "scripts/build_release.ps1" '\[string\]\$Version = ".*?"' "[string]`$Version = `"$TargetVersion`""
Replace-InFile "scripts/build_release.ps1" 'LocalLLMServerManager v\d+\.\d+\.\d+' "LocalLLMServerManager v$TargetVersion"

# 7. Avalonia UI Views
Replace-InFile "App.axaml" 'ToolTipText="Local LLM Server Manager v.*?"' "ToolTipText=`"Local LLM Server Manager v$TargetVersion`""
Replace-InFile "Views/MainWindow.axaml" 'Title="Local LLM Server Manager v.*?"' "Title=`"Local LLM Server Manager v$TargetVersion`""
Replace-InFile "Views/MainWindow.axaml" '<TextBlock Text="v\d+\.\d+\.\d+"' "<TextBlock Text=`"v$TargetVersion`""
Replace-InFile "LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml" 'Manager v\d+\.\d+\.\d+' "Manager v$TargetVersion"
Replace-InFile "LocalLLMServerManager.Shared/Views/Controls/DynamicStageContainerControl.axaml" 'LocalLLMServerManager v\d+\.\d+\.\d+' "LocalLLMServerManager v$TargetVersion"

# 8. User Documentation
Replace-InFile "docs/USER_GUIDE.md" 'Local LLM Server Manager \(v\d+\.\d+\.\d+\)' "Local LLM Server Manager (v$TargetVersion)"

# 9. Test Assertions
Replace-InFile "LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs" 'Assert\.Contains\("v\d+\.\d+\.\d+", versionTextBlock\.Text\);' "Assert.Contains(`"v$TargetVersion`", versionTextBlock.Text);"
Replace-InFile "LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs" 'Assert\.Equal\("\d+\.\d+\.\d+", loadedVersion\);' "Assert.Equal(`"$TargetVersion`", loadedVersion);"
Replace-InFile "LocalLLMServerManager.Tests/WasmAssetFreshnessTests.cs" '\?\? "\d+\.\d+\.\d+";' "?? `"$TargetVersion`";"

Write-Host "Version identifiers successfully bumped to v$TargetVersion!" -ForegroundColor Green
Write-Host ""

# 4. Run Code Quality & Verification
Write-Host "--> 2. Running Quality Checks (Lint & Typecheck)..." -ForegroundColor Yellow
npm run lint
if ($LASTEXITCODE -ne 0) { throw "npm run lint failed!" }

npx tsc --noEmit
if ($LASTEXITCODE -ne 0) { throw "npx tsc --noEmit failed!" }

Write-Host "--> 3. Refreshing Fast Update Application Bundle..." -ForegroundColor Yellow
pwsh (Join-Path $RootDir "scripts/fast_update.ps1") -NoLaunch
if ($LASTEXITCODE -ne 0) { throw "fast_update.ps1 failed!" }

Write-Host "--> 4. Running Unit & Integration Tests..." -ForegroundColor Yellow
dotnet test (Join-Path $RootDir "LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj") --filter "FullyQualifiedName~AvaloniaHeadlessInteractionTests.MainWindow_HasExpectedChromeAndTitle|FullyQualifiedName~SettingsViewModelTests" --nologo -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed!" }

# 5. Git Commit & Tag
if (-not $NoCommit) {
    Write-Host "--> 5. Staging & Committing Version Bump..." -ForegroundColor Yellow
    git add -A
    git commit -m "chore(release): bump version to v$TargetVersion"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed!" }

    if (-not $NoTag) {
        Write-Host "--> 6. Creating Git Tag v$TargetVersion..." -ForegroundColor Yellow
        git tag -a "v$TargetVersion" -m "Release v$TargetVersion" -f
        if ($LASTEXITCODE -ne 0) { throw "git tag failed!" }
    }

    if ($Push) {
        Write-Host "--> 7. Pushing Commit & Tags to origin/main..." -ForegroundColor Yellow
        git push origin main --tags
        if ($LASTEXITCODE -ne 0) { throw "git push failed!" }
        Write-Host "Pushed v$TargetVersion to GitHub! Release pipeline triggered." -ForegroundColor Green
    } else {
        Write-Host "Commit & Tag created locally. Run 'git push origin main --tags' when ready to deploy." -ForegroundColor Cyan
    }
}

# 6. Local Installer Build (Optional)
if ($BuildInstaller) {
    Write-Host "--> 8. Building Local Release Package & Installer..." -ForegroundColor Yellow
    pwsh (Join-Path $RootDir "scripts/build_release.ps1") -Version $TargetVersion
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Release Pipeline Complete: v$TargetVersion" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
