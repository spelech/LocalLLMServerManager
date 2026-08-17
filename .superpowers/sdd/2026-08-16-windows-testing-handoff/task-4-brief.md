# Task 4 Brief: Native Runtime Smoke Check & Background Service Verification

**Files:**
- Executable: `publish/LocalLLMServerManager.exe`

**Global Constraints:**
- Target Framework: `net10.0`
- Web Server Port: 5246
- Platform: Windows 11 win-x64

**Requirements:**
1. Test CLI argument parsing:
   ```powershell
   .\publish\LocalLLMServerManager.exe --help
   ```
2. Launch background service instance on port 5246:
   ```powershell
   $proc = Start-Process -FilePath ".\publish\LocalLLMServerManager.exe" -ArgumentList "--service" -PassThru
   Start-Sleep -Seconds 3
   $health = Invoke-RestMethod -Uri "http://127.0.0.1:5246/health" -Method Get
   ```
   Verify response is HTTP 200 with `status: "Healthy"` and `version: "3.4.0"`.
3. Stop the service process cleanly:
   ```powershell
   Stop-Process -Id $proc.Id -Force
   ```
4. Verify that no lingering `LocalLLMServerManager` processes remain.
5. Write detailed report to `task-4-report.md`.
