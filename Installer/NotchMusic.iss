; Inno Setup Script for DRANOTCH
#define MyAppName "DRANOTCH"
#ifndef MyAppVersion
#define MyAppVersion "1.7.0"
#endif
#define MyAppPublisher "DRANOTCH"
#define MyAppURL "https://github.com/dningratx/dranotch"
#define MyAppExeName "NotchMusic.exe"

[Setup]
AppId={{E63BD772-BC81-426B-9989-138B426E778B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist\Installer
OutputBaseFilename=DRANOTCH-Setup-{#MyAppVersion}
SetupIconFile=..\src\NotchMusic\Assets\app.ico
UninstallDisplayIcon={app}\app.ico
ChangesAssociations=yes
Compression=lzma
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=*.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\NotchMusic\Assets\app.ico"; DestDir: "{app}"; DestName: "app.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill process if running before uninstallation
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "StopDRANOTCH"

[UninstallDelete]
; Clean up app binaries (leaves user data in LocalAppData\DRANOTCH untouched)
Type: filesandordirs; Name: "{app}"

[Registry]
; Clean up startup entry upon uninstall if user enabled it
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "DRANOTCH"; Flags: dontcreatekey uninsdeletevalue

[Code]
procedure SHChangeNotify(wEventId: Integer; uFlags: Cardinal; dwItem1, dwItem2: Integer);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    try
      // Flush Windows Shell Icon and Association cache immediately
      SHChangeNotify($08000000, 0, 0, 0);
    except
    end;
  end;
end;
