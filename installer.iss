; Inno Setup 7 Script for JobAppTracker
; Generates self-contained 64-bit Windows installer

#define MyAppName "JobAppTracker"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "JobAppTracker"
#define MyAppExeName "JobAppTracker.exe"

[Setup]
; Application metadata
AppId={{D37E64B2-9F18-4E4A-B68C-26F8246E7199}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE

; Visual styling and icons
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

; Output directory & binary name
OutputDir=dist
OutputBaseFilename=JobAppTrackerSetup
Compression=lzma2/max
SolidCompression=yes

; Architecture (64-bit native Windows)
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Package all files produced by dotnet publish
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
