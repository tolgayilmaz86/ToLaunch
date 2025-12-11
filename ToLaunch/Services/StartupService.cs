using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ToLaunch.Services;

/// <summary>
/// Service for managing Windows startup registration.
/// </summary>
public static class StartupService
{
    private const string AppName = "ToLaunch";
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Sets whether the application should start with Windows.
    /// </summary>
    /// <param name="enable">True to enable startup with Windows, false to disable.</param>
    /// <returns>True if the operation succeeded, false otherwise.</returns>
    public static bool SetStartWithWindows(bool enable)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
            if (key == null)
            {
                LogService.LogWarning("Failed to open registry key for startup.");
                return false;
            }

            if (enable)
            {
                // Get the path to the current executable
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    LogService.LogError("Failed to get executable path.");
                    return false;
                }

                // Add to startup with quoted path and --startup argument to indicate startup launch
                key.SetValue(AppName, $"\"{exePath}\" --startup");
                LogService.LogInfo($"Added ToLaunch to Windows startup: {exePath}");
            }
            else
            {
                // Remove from startup
                key.DeleteValue(AppName, throwOnMissingValue: false);
                LogService.LogInfo("Removed ToLaunch from Windows startup.");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set startup registry: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if the application is currently set to start with Windows.
    /// </summary>
    /// <returns>True if startup is enabled, false otherwise.</returns>
    public static bool IsStartWithWindowsEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: false);
            if (key == null)
                return false;

            var value = key.GetValue(AppName);
            return value != null;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to check startup registry: {ex.Message}");
            return false;
        }
    }
}
