# ToLaunch Installer

This folder contains all installer-related files for ToLaunch. All installer operations are self-contained here and don't affect the main project root.

## Folder Structure

```
Installer/
??? build.bat        # Build script - run this to create installer
??? ToLaunch.iss     # Inno Setup script configuration
??? README.md        # This file
??? .gitignore       # Ignores output and publish folders
??? publish/         # (generated) Published application files
??? output/          # (generated) Final installer executable
```

## Prerequisites

### 1. Inno Setup 6

Download and install from: https://jrsoftware.org/isdl.php

The build script automatically checks these locations:
- `C:\Program Files (x86)\Inno Setup 6\`
- `C:\Program Files\Inno Setup 6\`
- `%LOCALAPPDATA%\Programs\Inno Setup 6\`

### 2. .NET 8 SDK

Required to publish the application:
```cmd
dotnet --version
```

## Building the Installer

Simply run from this folder:
```cmd
build.bat
```

This will:
1. Clean any previous build output
2. Publish the ToLaunch application to `publish/`
3. Create the installer in `output/`
4. Open the output folder when complete

## Output

The installer is created at:
```
Installer/output/ToLaunch-Setup-0.1.0.exe
```

## Installer Features

### .NET Runtime Check
- Automatically detects if .NET 8 Desktop Runtime is installed
- Opens official Microsoft download page if missing
- User can choose to download now, continue anyway, or cancel

### What Gets Installed

```
C:\Program Files\ToLaunch\
??? ToLaunch.exe                 (Application)
??? ToLaunch.dll                 (Application library)
??? ToLaunch.runtimeconfig.json  (Runtime config)
??? ToLaunch.deps.json           (Dependencies info)
??? ToLaunch.ico                 (Application icon)
??? Avalonia.dll                 (and ~45 other DLLs)
??? LICENSE                      (License file)
??? README.md                    (Documentation)
```

### Shortcuts Created
- Start Menu: ToLaunch
- Desktop (optional)
- Uninstaller in Start Menu and Windows Settings

## Customization

### Update Version Number

Edit `ToLaunch.iss`:
```ini
#define MyAppVersion "0.1.0"
```

Also update in `ToLaunch\ToLaunch.csproj`:
```xml
<Version>0.1.0</Version>
```

### Change .NET Runtime Download URL

Edit the constants in `ToLaunch.iss`:
```pascal
const
  DOTNET8_DESKTOP_RUNTIME_URL = 'https://dotnet.microsoft.com/download/dotnet/8.0';
  DOTNET8_DESKTOP_RUNTIME_DIRECT_DOWNLOAD = 'https://dotnet.microsoft.com/...';
```

### Add Additional Files

Edit `[Files]` section in `ToLaunch.iss`:
```ini
Source: "..\path\to\file"; DestDir: "{app}"; Flags: ignoreversion
```

## Troubleshooting

### "Inno Setup not found"
- Install Inno Setup 6 from https://jrsoftware.org/isdl.php
- If installed elsewhere, update `build.bat` with the correct path

### "Source file not found"
- Run `build.bat` from the Installer folder
- Make sure the publish step completed successfully
- Verify `publish/` folder exists with all files

### Application won't start after install
- Check if .NET 8 Desktop Runtime is installed
- Verify all files are in the installation folder
- Check Windows Event Viewer for errors

## Code Signing (Optional)

For production releases, sign your installer:

```cmd
signtool sign /f "certificate.pfx" /p "password" /t http://timestamp.digicert.com "output\ToLaunch-Setup-0.1.0.exe"
```

This prevents Windows SmartScreen warnings and increases user trust.
