; Build with installer/build.ps1. Inno Setup 6.3+ or 7 is supported.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef FileVersion
  #define FileVersion "1.0.0.0"
#endif
#ifndef PublishDir
  #error PublishDir must point to the self-contained publish directory.
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir SourcePath + "..\artifacts"
#endif

[Setup]
AppId={{57A547F4-6D3E-4B71-9E7E-B0C9B2C2B496}
AppName=Drag Tree
AppVersion={#AppVersion}
AppVerName=Drag Tree {#AppVersion}
AppPublisher=MrFrickinFixit
VersionInfoVersion={#FileVersion}
VersionInfoProductName=Drag Tree
VersionInfoDescription=Drag Tree reaction trainer installer
DefaultDirName={localappdata}\Programs\Drag Tree
DefaultGroupName=Drag Tree
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
WizardStyle=modern
OutputDir={#InstallerOutputDir}
OutputBaseFilename=DragTree-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
UninstallDisplayIcon={app}\DragTree.exe
CloseApplications=yes
RestartApplications=no
DisableDirPage=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,keys.json"

[Icons]
Name: "{autoprograms}\Drag Tree"; Filename: "{app}\DragTree.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Drag Tree"; Filename: "{app}\DragTree.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\DragTree.exe"; Description: "Launch Drag Tree"; Flags: nowait postinstall skipifsilent

; Preferences live outside {app}, in %LOCALAPPDATA%\DragTree.
; Uninstall intentionally preserves the racer's selected keys.
