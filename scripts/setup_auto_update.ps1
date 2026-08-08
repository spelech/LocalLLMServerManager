$ErrorActionPreference = "Stop"

# Check for Admin privileges
$user = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $user.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Administrator privileges are required to create a Scheduled Task."
    Write-Warning "Please restart this PowerShell session as Administrator and run the script again."
    exit 1
}

$TaskName = "LocalLLMServerManager_AutoUpdate"
$ActionPath = "powershell.exe"
$UpdateScript = Join-Path $PSScriptRoot "update.ps1"
$ActionArgs = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$UpdateScript`""

Write-Host "Registering Auto-Update Scheduled Task..." -ForegroundColor Cyan

# Create the action
$Action = New-ScheduledTaskAction -Execute $ActionPath -Argument $ActionArgs -WorkingDirectory $PSScriptRoot

# Create a daily trigger (e.g., at 3:00 AM)
$Trigger = New-ScheduledTaskTrigger -Daily -At 3:00AM

# Run with highest privileges as the SYSTEM account so it can stop/start services without being logged in
$Principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest

# Task settings (allow it to run on demand, stop if running too long, etc.)
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 30)

# Register the task
Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings -Force | Out-Null

Write-Host "Success! The service will now automatically check for updates and build them every day at 3:00 AM." -ForegroundColor Green
Write-Host "You can manually trigger the update anytime by running: Start-ScheduledTask -TaskName `"$TaskName`"" -ForegroundColor Yellow
