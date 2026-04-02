#define MyAppName "EchoX"
#define MyAppPublisher "Shanto Joseph"
#define MyAppURL "https://github.com/shanto-joseph/EchoX"
#define MyAppSupportURL "https://coffee.shantojoseph.com/"
#define MyAppExeName "EchoX.exe"
#define MyAppCopyright "Copyright (C) 2026 Shanto Joseph"
#define MyAppBinaryPath AddBackslash(SourcePath) + "..\\bin\\Release\\net48-windows\\" + MyAppExeName
#define MyAppVersionFull GetVersionNumbersString(MyAppBinaryPath)
#if Len(MyAppVersionFull) == 0
  #error "Could not read version from the built EchoX.exe. Build the app first."
#endif
#define MyAppVersion Copy(MyAppVersionFull, 1, RPos(".", MyAppVersionFull) - 1)

[Setup]
AppId={{6D8E3D55-0C3C-4A4A-B6AB-5D514E87F4A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AppMutex=Local\EchoX.SingleInstance
LicenseFile=..\LICENSE
OutputDir=..\dist\installer
OutputBaseFilename=EchoX-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\LOGO\EchoX_exe.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
ChangesAssociations=no
CloseApplications=yes
VersionInfoVersion={#MyAppVersionFull}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersionFull}
VersionInfoProductTextVersion={#MyAppVersion}
VersionInfoOriginalFileName=EchoX-Setup-{#MyAppVersion}.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startup"; Description: "Launch EchoX on startup"; GroupDescription: "Additional options:"

[Files]
Source: "..\bin\Release\net48-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: binary; ValueName: "{#MyAppName}"; ValueData: "02 00 00 00 00 00 00 00 00 00 00 00"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  RemoveUserData: Boolean;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveUserData :=
      MsgBox(
        'Do you want to remove all EchoX user data too?' + #13#10#13#10 +
        'This will delete saved profiles, settings, keybinds, cache, and other data stored in Local AppData.',
        mbConfirmation, MB_YESNO) = IDYES;

    if RemoveUserData then
      DelTree(ExpandConstant('{localappdata}\EchoX'), True, True, True);
  end;
end;
