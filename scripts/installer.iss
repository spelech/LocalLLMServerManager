; Script generated for Inno Setup - LocalLLMServerManager v3.5.0
#define MyAppName "Local LLM Server Manager"
#define MyAppVersion "3.5.0"
#define MyAppPublisher "LocalLLMServerManager Team"
#define MyAppURL "https://github.com/spelech/LocalLLMServerManager"
#define MyAppExeName "LocalLLMServerManager.exe"

[Setup]
AppId={{D1A39E4C-6721-4E12-A349-8F8D58014E7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\LocalLLMServerManager
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\README.md
OutputBaseFilename=LocalLLMServerManager-v{#MyAppVersion}-Setup
OutputDir=..\dist
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
SetupIconFile=..\Assets\app-icon.ico
CloseApplications=yes
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Auto-start System Tray App on user login"; GroupDescription: "System Integration"
Name: "windowsservice"; Description: "Install background Windows Service (starts automatically on system boot)"; GroupDescription: "System Integration"; Flags: checkedonce

[Files]
; Main published application files (excluding settings.json so existing user settings are preserved on upgrade)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "settings.json"
; Preserve settings.json across in-place upgrades (only install if not already present, never delete on uninstall)
Source: "..\publish\settings.json*"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall; Permissions: users-full
Source: "..\Assets\app-icon.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app-icon.ico"
Name: "{group}\Open Web Dashboard"; Filename: "http://127.0.0.1:5246"; IconFilename: "{app}\Assets\app-icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\app-icon.ico"

[Registry]
; Auto-start System Tray App on User Logon
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LocalLLMServerManagerTray"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; Install / Reconfigure & Start Windows Service if selected
Filename: "sc.exe"; Parameters: "create LocalLLMServerManager binPath= """"{app}\{#MyAppExeName}"" --service"" start= auto displayName= ""Local LLM Server Manager"""; Tasks: windowsservice; Flags: runhidden
Filename: "sc.exe"; Parameters: "config LocalLLMServerManager binPath= """"{app}\{#MyAppExeName}"" --service"" start= auto displayName= ""Local LLM Server Manager"""; Tasks: windowsservice; Flags: runhidden
Filename: "sc.exe"; Parameters: "description LocalLLMServerManager ""Orchestrates GPU VRAM between Ollama and Forge, and manages local model weights."""; Tasks: windowsservice; Flags: runhidden
Filename: "net.exe"; Parameters: "start LocalLLMServerManager"; Tasks: windowsservice; Flags: runhidden
; Launch Tray App after installation completes
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop and remove Windows Service on uninstall
Filename: "net.exe"; Parameters: "stop LocalLLMServerManager"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete LocalLLMServerManager"; Flags: runhidden
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName} /T"; Flags: runhidden

[Code]
// Helper function to check if the LocalLLMServerManager Windows Service exists
function ServiceExists(const ServiceName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function ServiceNotExists(const ServiceName: String): Boolean;
begin
  Result := not ServiceExists(ServiceName);
end;

// PrepareToInstall is called before file extraction begins.
// Gracefully stops the Windows Service and terminates active tray application processes
// to eliminate "Error 32: The process cannot access the file because it is being used by another process".
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';

  // 1. Stop background Windows Service if running
  Exec(ExpandConstant('{sys}\net.exe'), 'stop LocalLLMServerManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop LocalLLMServerManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // 2. Terminate any running LocalLLMServerManager.exe processes (tray app or previous instances)
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // 3. Allow Windows kernel time to close file handles
  Sleep(500);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{sys}\net.exe'), 'stop LocalLLMServerManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end;
end;
