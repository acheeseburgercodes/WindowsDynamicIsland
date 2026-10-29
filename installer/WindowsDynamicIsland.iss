#ifndef MyAppVersion
  #define MyAppVersion "0.3.1"
#endif

#ifndef RepoRoot
  #define RepoRoot ".."
#endif

#ifndef StageDir
  #define StageDir RepoRoot + "\artifacts\installer-stage"
#endif

#define MyAppName "Windows Dynamic Island"
#define MyAppPublisher "acheeseburgercodes"
#define MyAppURL "https://github.com/acheeseburgercodes/WindowsDynamicIsland"
#define MyAppExeName "DynamicIsland.exe"

[Setup]
AppId={{F4088A10-DDE8-4EB5-A747-9F85AD8EEB33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\Windows Dynamic Island
DefaultGroupName=Windows Dynamic Island
DisableProgramGroupPage=yes
LicenseFile={#RepoRoot}\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
OutputDir={#RepoRoot}\artifacts\installer
OutputBaseFilename=WindowsDynamicIsland-v{#MyAppVersion}-win-x64-setup
SetupIconFile={#RepoRoot}\IslandSettings\windows\IslandSettings\IslandSettings.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; When Build-Installer.ps1 is run with -CertificateThumbprint it passes the
; IslandSign tool on the ISCC command line; the uninstaller stub generated at
; compile time is then signed too, so Smart App Control sees trusted code for
; both install and uninstall.
#ifdef CodeSigningThumbprint
SignTool=IslandSign
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start Windows Dynamic Island when I sign in"; GroupDescription: "Startup:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#StageDir}\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\control-center\*"; DestDir: "{app}\IslandSettings"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoRoot}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\legal\*"; DestDir: "{app}\legal"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Dynamic Island"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Island Control Center"; Filename: "{app}\IslandSettings\IslandSettings.exe"
Name: "{group}\Uninstall Windows Dynamic Island"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Windows Dynamic Island"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WindowsDynamicIsland"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startup; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\dynamicisland-settings"; ValueType: string; ValueData: "URL:Dynamic Island Settings Protocol"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\dynamicisland-settings"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKLM; Subkey: "Software\Classes\dynamicisland-settings\DefaultIcon"; ValueType: string; ValueData: """{app}\IslandSettings\IslandSettings.exe"",0"
Root: HKLM; Subkey: "Software\Classes\dynamicisland-settings\shell\open\command"; ValueType: string; ValueData: """{app}\IslandSettings\IslandSettings.exe"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Windows Dynamic Island"; Flags: nowait postinstall skipifsilent

[Code]
// Close the running app before uninstalling. Otherwise the locked
// DynamicIsland.exe is left behind in {app} with no registry entries,
// shortcuts or Control Panel registration - the app keeps running but
// disappears from Start Menu and installed apps.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec('taskkill.exe', '/F /IM DynamicIsland.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/F /IM IslandSettings.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
