#ifndef PayloadDir
  #error PayloadDir must point to a verified Vela build.
#endif
#ifndef OutputDir
  #error OutputDir must be specified.
#endif
#ifndef BuildArch
  #error BuildArch must be x64 or arm64.
#endif
#ifndef AppVersion
  #error AppVersion must be specified.
#endif

[Setup]
AppId=Vela.Utilities.Setup
AppName=Vela for Windows
AppVersion={#AppVersion}
AppPublisher=snikhil-10
AppPublisherURL=https://github.com/snikhil-10/Vela
DefaultDirName={localappdata}\Programs\Vela
DefaultGroupName=Vela
PrivilegesRequired=lowest
MinVersion=10.0.22621
#if BuildArch == "arm64"
ArchitecturesAllowed=arm64 and x64compatible
#else
ArchitecturesAllowed=x64compatible
#endif
OutputDir={#OutputDir}
OutputBaseFilename=Vela-Setup-{#AppVersion}-{#BuildArch}
SetupIconFile=..\..\src\Vela.Windows\Assets\Vela.ico
UninstallDisplayIcon={app}\Assets\Vela.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
AppMutex=Local\Vela.Utilities.Desktop.Instance

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "\docs\*,\validation\*,\feature-audit.json,\Install.cmd,\Uninstall.cmd,*.md"; Flags: ignoreversion recursesubdirs
Source: "{#PayloadDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\NOTICE.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Vela"; Filename: "{app}\Vela.Windows.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\Vela.ico"
Name: "{autodesktop}\Vela"; Filename: "{app}\Vela.Windows.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\Vela.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\Vela.Windows.exe"; Description: "Launch Vela"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := not RegKeyExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Vela.Utilities');
  if not Result then
    MsgBox('An older Vela installation was made with Install.cmd. Uninstall it first using Windows Installed apps; your Vela settings will be kept. Then run this installer again.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Vela.Utilities', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\Vela.Windows.exe') + '" --startup') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Vela.Utilities');
end;
