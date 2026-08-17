# Task 3 Brief: Windows Native Packaging & Inno Setup Compilation

**Files:**
- Script: `scripts/build_release.ps1`
- Script: `scripts/installer.iss`
- Output: `publish/`, `dist/LocalLLMServerManager-v3.4.0-win-x64.zip`, `dist/LocalLLMServerManager-v3.4.0-Setup.exe`

**Global Constraints:**
- Target Framework: `net10.0`
- Target Runtime: `win-x64` (self-contained)
- Platform: Windows 11 win-x64

**Requirements:**
1. Execute `pwsh scripts/build_release.ps1`.
2. Verify that:
   - Avalonia WASM is compiled and synced to `wwwroot/`.
   - `publish/` directory contains self-contained `LocalLLMServerManager.exe`.
   - `dist/LocalLLMServerManager-v3.4.0-win-x64.zip` is created with non-zero size.
   - Inno Setup installer compilation status is recorded (if ISCC.exe is found).
3. Record file sizes and timestamps in `task-3-report.md`.
