[Setup]
AppName=ToLaunch
AppVersion={#AppVersion}
DefaultDirName={pf}\ToLaunch
DefaultGroupName=ToLaunch
OutputDir=.
OutputBaseFilename=ToLaunch-Setup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\ToLaunch"; Filename: "{app}\ToLaunch.exe"
Name: "{commondesktop}\ToLaunch"; Filename: "{app}\ToLaunch.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ToLaunch.exe"; Description: "{cm:LaunchProgram,ToLaunch}"; Flags: nowait postinstall skipifsilent