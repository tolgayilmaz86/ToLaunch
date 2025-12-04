@echo off
REM =====================================
REM ToLaunch Installer Build Script
REM =====================================
REM This script publishes the application and creates the installer
REM All output stays within the Installer folder
setlocal enabledelayedexpansion

echo =====================================
echo ToLaunch - Installer Builder
echo =====================================
echo.

REM Determine script location and set paths
set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%.."
set "PUBLISH_DIR=%SCRIPT_DIR%publish"

REM Check if Inno Setup is installed (try common locations)
set "INNO_SETUP_PATH="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "INNO_SETUP_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "INNO_SETUP_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"
) else if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    set "INNO_SETUP_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
)

if "!INNO_SETUP_PATH!"=="" (
    echo [ERROR] Inno Setup 6 not found!
    echo.
    echo Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php
    echo.
    echo Checked locations:
    echo   - C:\Program Files ^(x86^)\Inno Setup 6\
    echo   - C:\Program Files\Inno Setup 6\
    echo   - %LOCALAPPDATA%\Programs\Inno Setup 6\
    echo.
    pause
    exit /b 1
)

echo [OK] Found Inno Setup at: !INNO_SETUP_PATH!
echo.

REM =====================================
REM Step 1: Publish the application
REM =====================================
echo Step 1/2: Publishing application...
echo.

REM Clean previous publish (inside Installer folder)
if exist "!PUBLISH_DIR!" (
    echo Cleaning previous publish folder...
    rmdir /s /q "!PUBLISH_DIR!"
)

REM Publish the application (framework-dependent, requires .NET 8 runtime)
echo Running: dotnet publish -c Release -r win-x64 -o "!PUBLISH_DIR!"
dotnet publish "!PROJECT_ROOT!\ToLaunch\ToLaunch.csproj" -c Release -r win-x64 -o "!PUBLISH_DIR!"

if !ERRORLEVEL! NEQ 0 (
    echo.
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

echo.
echo [OK] Publish completed successfully
echo.

REM Verify publish output
if not exist "!PUBLISH_DIR!\ToLaunch.exe" (
    echo [ERROR] ToLaunch.exe not found in publish folder!
    pause
    exit /b 1
)

if not exist "!PUBLISH_DIR!\ToLaunch.dll" (
    echo [ERROR] ToLaunch.dll not found in publish folder!
    pause
    exit /b 1
)

echo Publish folder contents:
for /f %%a in ('dir /b /a-d "!PUBLISH_DIR!\*.dll" 2^>nul ^| find /c /v ""') do echo   - %%a DLL files
for /f %%a in ('dir /b /a-d "!PUBLISH_DIR!\*.exe" 2^>nul ^| find /c /v ""') do echo   - %%a EXE files
echo.

REM =====================================
REM Step 2: Build the installer
REM =====================================
echo Step 2/2: Building installer with Inno Setup...
echo.

REM Create output directory if it doesn't exist
if not exist "%SCRIPT_DIR%output" mkdir "%SCRIPT_DIR%output"

REM Run Inno Setup compiler
"!INNO_SETUP_PATH!" "%SCRIPT_DIR%ToLaunch.iss"

if !ERRORLEVEL! EQU 0 (
    echo.
    echo =====================================
    echo SUCCESS! Installer created
    echo =====================================
    echo.
    echo Output location: %SCRIPT_DIR%output\
    echo.
    
    REM List created files
    echo Created files:
    for %%f in ("%SCRIPT_DIR%output\*.exe") do echo   - %%~nxf
    echo.
    
    REM Open output folder
    explorer "%SCRIPT_DIR%output"
) else (
    echo.
    echo =====================================
    echo FAILED! Check errors above
    echo =====================================
    echo.
    pause
    exit /b 1
)

echo.
pause
endlocal
