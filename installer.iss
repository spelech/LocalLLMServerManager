; Script generated for Inno Setup - LocalLLMServerManager v2.1.0
#define MyAppName "Local LLM Server Manager"
#define MyAppVersion "2.1.0"
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
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=README.md
OutputBaseFilename=LocalLLMServerManager-v{#MyAppVersion}-Setup
OutputDir=.
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Auto-start System Tray App on user login"; GroupDescription: "System Integration"; Flags: checked
Name: "windowsservice"; Description: "Install background Windows Service (starts automatically on system boot)"; GroupDescription: "System Integration"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Open Web Dashboard"; Filename: "http://127.0.0.1:5246"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start System Tray App on User Logon
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LocalLLMServerManagerTray"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; Install & Start Windows Service if selected
Filename: "sc.exe"; Parameters: "create LocalLLMServerManager binPath= """"{app}\{#MyAppExeName}"" --service"" start= auto displayName= ""Local LLM Server Manager"""; Tasks: windowsservice; Flags: runhidden
Filename: "net.exe"; Parameters: "start LocalLLMServerManager"; Tasks: windowsservice; Flags: runhidden
; Launch Tray App after installation completes
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop and remove Windows Service on uninstall
Filename: "net.exe"; Parameters: "stop LocalLLMServerManager"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete LocalLLMServerManager"; Flags: runhidden
