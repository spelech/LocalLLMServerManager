# Design Document: Cross-Platform Linux & Remote SSH Workflow for LocalLLMServerManager

## Goal
Enable seamless building, running, and user interface interaction for `LocalLLMServerManager` on Linux. Support both direct desktop native Avalonia UI execution and SSH remote viewing/management via the integrated ASP.NET Core Web UI dashboard.

---

## Technical Challenges & Proposed Solutions

### 1. MSBuild Path Separator Compatibility on Linux
* **Issue:** `LocalLLMServerManager.csproj` currently uses Windows-style backslashes in item exclusions (e.g., `LocalLLMServerManager.Shared\**`), which breaks MSBuild's `GenerateAvaloniaResourcesTask` on Linux by causing glob mismatch errors (`System.IO.FileNotFoundException`).
* **Solution:** Convert all MSBuild project file path separators to standard cross-platform forward slashes (`/`).

### 2. GPU Hardware Telemetry Abstraction
* **Issue:** GPU VRAM detection relies on `System.Management` (WMI) and Windows Registry (`HKLM`), which throw `PlatformNotSupportedException` on Linux.
* **Solution:** Implement an OS check (`OperatingSystem.IsLinux()`):
  * On Linux, query `nvidia-smi` CLI (`nvidia-smi --query-gpu=name,memory.total,memory.used,memory.free --format=csv,noheader,nounits`) to gather real-time GPU VRAM telemetry.
  * If `nvidia-smi` is unavailable or fails, fall back to standard Linux system memory `/proc/meminfo`.

### 3. Headless & SSH Remote Viewing Workflow
* **Local Linux Desktop Session:** Run `dotnet run` from terminal. Avalonia UI initializes via X11/Wayland with full native dark window controls.
* **Remote SSH Session:** Run `dotnet run -- --service` or `dotnet run` (headless mode). Connect over SSH with local port forwarding:
  ```bash
  ssh -L 5246:localhost:5246 user@linux-host
  ```
  Open `http://localhost:5246` in any browser to access 100% of the UI features (VRAM monitor, Hugging Face search, CivitAI downloader, 3D WebGL viewer).

---

## Verification Plan

1. **Build Verification:** Run `dotnet build` on Linux and ensure 0 compilation errors.
2. **Headless Web UI Verification:** Run `dotnet run -- --service` and execute `curl -i http://localhost:5246` to verify HTTP 200 OK.
3. **Native Desktop Verification:** Verify Avalonia UI launches on Linux desktop when display server (X11/Wayland) is active.
