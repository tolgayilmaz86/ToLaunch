; ToLaunch Installer Script for Inno Setup
; Requires Inno Setup 6.0 or later
; Download from: https://jrsoftware.org/isdl.php

#define MyAppName "ToLaunch"
#define MyAppVersion "0.6.0"
#define MyAppPublisher "ToLaunch Contributors"
#define MyAppURL "https://github.com/tolgayilmaz86/ToLaunch"
#define MyAppExeName "ToLaunch.exe"

; Paths relative to this .iss file location (installer folder)
#define SourcePath "publish"
#define OutputPath "output"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
AppId={{A5F8B2C1-3D4E-5F6A-7B8C-9D0E1F2A3B4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputPath}
OutputBaseFilename=ToLaunch-Setup-{#MyAppVersion}
SetupIconFile=..\ToLaunch\Assets\ToLaunch.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenu"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; All application files (flat structure - no libs subfolder)
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Application icon for shortcuts
Source: "..\ToLaunch\Assets\ToLaunch.ico"; DestDir: "{app}"; Flags: ignoreversion

; License and documentation
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\ToLaunch.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\ToLaunch.ico"; Tasks: desktopicon
Name: "{autostartmenu}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\ToLaunch.ico"; Tasks: startmenu

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// .NET 8 Runtime check and installation prompt
const
  DOTNET8_DESKTOP_RUNTIME_URL = 'https://dotnet.microsoft.com/download/dotnet/8.0';
  DOTNET8_DESKTOP_RUNTIME_DIRECT_DOWNLOAD = 'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.11-windows-x64-installer';

function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
  TempFile: String;
begin
  Result := False;
  
  // Create a temporary file to store the output
  TempFile := ExpandConstant('{tmp}\dotnet-version.txt');
  
  // Run dotnet --list-runtimes and save output to file
  if Exec('cmd.exe', '/C dotnet --list-runtimes > "' + TempFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // Load the output from the temp file
    if LoadStringFromFile(TempFile, Output) then
    begin
      // Check if .NET 8 Desktop Runtime is listed
      // Looking for "Microsoft.WindowsDesktop.App 8."
      if Pos('Microsoft.WindowsDesktop.App 8.', String(Output)) > 0 then
        Result := True;
    end;
    DeleteFile(TempFile);
  end;
end;

function InitializeSetup: Boolean;
var
  ButtonPressed: Integer;
  ResultCode: Integer;
begin
  Result := True;
  
  if not IsDotNet8DesktopRuntimeInstalled then
  begin
    ButtonPressed := MsgBox(
      'This application requires .NET 8 Desktop Runtime, which is not currently installed on your system.' + #13#10 + #13#10 +
      'You can:' + #13#10 +
      '  • Click YES to open the download page and install it now (recommended)' + #13#10 +
      '  • Click NO to continue installation anyway (app will not run until runtime is installed)' + #13#10 +
      '  • Click CANCEL to abort the installation' + #13#10 + #13#10 +
      'The .NET 8 Desktop Runtime is a free download from Microsoft (approximately 50 MB).',
      mbInformation,
      MB_YESNOCANCEL
    );
    
    case ButtonPressed of
      IDYES:
        begin
          // Open the .NET download page
          ShellExec('open', DOTNET8_DESKTOP_RUNTIME_DIRECT_DOWNLOAD, '', '', SW_SHOW, ewNoWait, ResultCode);
          
          MsgBox(
            'Please install the .NET 8 Desktop Runtime from the browser window that just opened.' + #13#10 + #13#10 +
            'After installing the runtime, click OK to continue with ToLaunch installation.',
            mbInformation,
            MB_OK
          );
        end;
      IDNO:
        begin
          // User chose to continue without runtime - warn them
          MsgBox(
            'Installation will continue, but ToLaunch will not run until you install .NET 8 Desktop Runtime.' + #13#10 + #13#10 +
            'You can download it later from:' + #13#10 +
            DOTNET8_DESKTOP_RUNTIME_URL,
            mbInformation,
            MB_OK
          );
        end;
      IDCANCEL:
        begin
          Result := False; // Abort installation
        end;
    end;
  end;
end;

// Show runtime download page after installation if not installed
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not IsDotNet8DesktopRuntimeInstalled then
    begin
      if MsgBox(
        'ToLaunch requires .NET 8 Desktop Runtime to run.' + #13#10 + #13#10 +
        'Would you like to download and install it now?',
        mbConfirmation,
        MB_YESNO
      ) = IDYES then
      begin
        ShellExec('open', DOTNET8_DESKTOP_RUNTIME_DIRECT_DOWNLOAD, '', '', SW_SHOW, ewNoWait, ResultCode);
      end;
    end;
  end;
end;
