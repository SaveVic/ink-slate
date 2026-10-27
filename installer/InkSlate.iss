; Inno Setup Script for InkSlate (Per-User / Non-Administrator Installer)
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{8B2577A7-44E8-41C2-88E7-7C4E612B712A}
AppName=InkSlate
AppVersion={#MyAppVersion}
AppPublisher=InkSlate Contributors
AppPublisherURL=https://github.com/SaveVic/ink-slate
AppSupportURL=https://github.com/SaveVic/ink-slate
AppUpdatesURL=https://github.com/SaveVic/ink-slate/releases
DefaultDirName={autopf}\InkSlate
ChangesAssociations=yes
DisableProgramGroupPage=yes
; Per-user install: No administrator UAC elevation prompt required
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=InkSlate-Setup-win-x64
SetupIconFile=..\InkSlate.App\Assets\inkslate.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\InkSlate"; Filename: "{app}\InkSlate.App.exe"
Name: "{autodesktop}\InkSlate"; Filename: "{app}\InkSlate.App.exe"; Tasks: desktopicon

[Registry]
; Per-User .notex File Association (Root: HKA with PrivilegesRequired=lowest maps to HKCU\Software\Classes)
Root: HKA; Subkey: "Software\Classes\.notex"; ValueType: string; ValueName: ""; ValueData: "InkSlate.Document"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\InkSlate.Document"; ValueType: string; ValueName: ""; ValueData: "InkSlate Document"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\InkSlate.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\InkSlate.App.exe,0"
Root: HKA; Subkey: "Software\Classes\InkSlate.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\InkSlate.App.exe"" ""%1"""

[Run]
Filename: "{app}\InkSlate.App.exe"; Description: "{cm:LaunchProgram,InkSlate}"; Flags: nowait postinstall skipifsilent

