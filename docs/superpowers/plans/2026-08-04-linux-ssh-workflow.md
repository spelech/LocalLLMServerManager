# Cross-Platform Linux & SSH Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable `LocalLLMServerManager` to build, run native Avalonia desktop UI on Linux, and support headless web UI SSH tunneling (`:5246`).

**Architecture:** Fix MSBuild path separators on Linux, abstract Windows-only hardware telemetry (`System.Management`) using `nvidia-smi` / Linux memory fallbacks.

**Tech Stack:** .NET 10 (C#), Avalonia UI 11.2.3, ASP.NET Core, YARP Reverse Proxy.

## Global Constraints
- Cross-platform path separators must use `/`.
- Operating system guards (`OperatingSystem.IsWindows()`, `OperatingSystem.IsLinux()`) must wrap OS-specific APIs.

---

### Task 1: Fix MSBuild Path Separators for Linux Build Compatibility

**Files:**
- Modify: `LocalLLMServerManager.csproj:40-55`

**Interfaces:**
- Consumes: MSBuild SDK & Avalonia Resource Compiler
- Produces: Clean cross-platform MSBuild resource inclusion

- [ ] **Step 1: Inspect `LocalLLMServerManager.csproj` line 40-55**
- [ ] **Step 2: Replace Windows backslashes `\` with `/` in Remove & Include items**
- [ ] **Step 3: Run `dotnet build` to verify clean build on Linux**

```bash
dotnet build /drives/nfs/repos/LocalLLMServerManager/LocalLLMServerManager.csproj
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 2: Implement Linux GPU Hardware Telemetry & Graceful Fallbacks

**Files:**
- Modify: `Program.cs` or Telemetry module

**Interfaces:**
- Consumes: `OperatingSystem.IsLinux()`, `nvidia-smi` process output
- Produces: GPU VRAM stats on Linux without `System.Management` crash

- [ ] **Step 1: Add Linux GPU detection using `nvidia-smi` process call when on Linux**
- [ ] **Step 2: Add fallback for system RAM when GPU detection is not supported**
- [ ] **Step 3: Run `dotnet build` and test execution**

```bash
dotnet build /drives/nfs/repos/LocalLLMServerManager/LocalLLMServerManager.csproj
```

---

### Task 3: Headless Service Verification & Web UI SSH Testing

**Files:**
- Test: ASP.NET Core listener on port 5246

- [ ] **Step 1: Run server in service mode**
```bash
dotnet run --project /drives/nfs/repos/LocalLLMServerManager/LocalLLMServerManager.csproj -- --service &
```
- [ ] **Step 2: Curl HTTP endpoint to verify dashboard availability**
```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5246
```
Expected: `200`
