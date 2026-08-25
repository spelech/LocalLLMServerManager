# Cross-Platform Tool Discovery, FFmpeg/Python Detection, Installers, and Linux Validation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Codify FFmpeg and Python audio tool detection/installation, enhance Linux and Windows search paths and runners, add firewall and service automation to installers, configure local WSL2 Linux testing, and establish a dual-OS CI matrix.

**Architecture:** Extend `IToolDiscoveryService` and `ToolDiscoveryService` with `DetectFFmpeg()` and `DetectPythonEnvironment()` supporting Windows and Linux (`/opt`, `/srv`, POSIX paths, shell runners `webui.sh`, `run.sh`). Update `install.ps1`, `installer.iss`, and `install_linux.sh` to automate firewall exceptions, service startup, and dependency installation. Set up .NET in WSL2 for local Linux execution and dual-OS GitHub Actions CI.

**Tech Stack:** C# / .NET 10.0, ASP.NET Core, Avalonia UI, PowerShell, Bash, Inno Setup, WSL2 Ubuntu, GitHub Actions.

---

### Task 1: Setup .NET SDK in Local WSL2 Ubuntu Environment

**Files:**
- WSL2 Environment: `wsl -d Ubuntu`

- [ ] **Step 1: Install .NET 10/8 SDK or required packages in WSL2 Ubuntu**
- [ ] **Step 2: Verify `dotnet --info` and `dotnet build` from WSL2 against `/mnt/c/Users/Alias/repos/LocalLLMServerManager`**

---

### Task 2: Enhance Tool Discovery for FFmpeg, Python Audio, and Linux Paths

**Files:**
- Modify: `Services/IToolDiscoveryService.cs`
- Modify: `Services/ToolDiscoveryService.cs`
- Modify: `Endpoints/DiscoveryEndpoints.cs`
- Test: `LocalLLMServerManager.Tests/ToolDiscoveryServiceTests.cs`

- [ ] **Step 1: Write unit tests for `DetectFFmpeg()` and `DetectPythonEnvironment()` across Windows and Linux search paths**
- [ ] **Step 2: Update `IToolDiscoveryService.cs` and `ToolDiscoveryService.cs` with `DetectFFmpeg()`, `DetectPythonEnvironment()`, Linux search roots (`/opt`, `/srv`, `/data`, `~/.local/share`), and shell runners (`webui.sh`, `webui-user.sh`, `run.sh`, `start.sh`)**
- [ ] **Step 3: Update `DiscoveryEndpoints.cs` to expose `ffmpeg` and `pythonEnvironment` in `/api/system/tools/detect`**
- [ ] **Step 4: Run unit tests on Windows and ensure all tests pass**

---

### Task 3: Enhance Windows & Linux Installer Scripts

**Files:**
- Modify: `scripts/install.ps1`
- Modify: `scripts/installer.iss`
- Modify: `scripts/install_linux.sh`

- [ ] **Step 1: Update `scripts/install.ps1` to support `-Firewall` (and interactive prompt), auto-start Windows Service with .NET host binPath, check/install `Gyan.FFmpeg` via winget, check/install Python audio packages when `-WithAudio` is selected, and print LAN connection URLs (`http://10.0.0.21:5246`)**
- [ ] **Step 2: Update `scripts/installer.iss` with a `firewall` task adding/removing inbound TCP 5246 rules and auto-start service task**
- [ ] **Step 3: Update `scripts/install_linux.sh` with FFmpeg apt/dnf installation, Python audio package installation, UFW/firewalld port 5246 opening, and systemd service generation**

---

### Task 4: Dual-OS CI Matrix in GitHub Actions

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Update `.github/workflows/ci.yml` with `strategy.matrix.os: [windows-latest, ubuntu-latest]` and OS-appropriate step execution for Playwright and tests**
- [ ] **Step 2: Verify workflow syntax and consistency**

---

### Task 5: Live Verification & Testing (Windows & WSL2 Linux)

**Files:**
- Execute in Windows: `dotnet test`, `npm run lint`, `npx tsc --noEmit`
- Execute in WSL2: `wsl -d Ubuntu -- bash -c "cd /mnt/c/Users/Alias/repos/LocalLLMServerManager && dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj -c Release"`

- [ ] **Step 1: Run complete unit test suite in Windows**
- [ ] **Step 2: Run complete unit test suite natively inside WSL2 Linux**
- [ ] **Step 3: Run `npm run lint` and `npx tsc --noEmit`**
- [ ] **Step 4: Commit and push changes to `main`**
