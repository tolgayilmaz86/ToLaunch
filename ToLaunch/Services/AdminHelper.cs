using System;
using System.Diagnostics;
using System.Security.Principal;

namespace ToLaunch.Services;

/// <summary>
/// Helper class for managing administrator privileges for the ToLaunch application.
/// </summary>
public static class AdminHelper
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts the current application with administrator privileges.
    /// Returns true if the restart was initiated, false if it failed or was cancelled.
    /// </summary>
    public static bool RestartAsAdministrator()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(exePath))
            {
                Debug.WriteLine("Could not determine executable path for elevation restart");
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC prompt
            Debug.WriteLine("User cancelled UAC prompt");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restart as administrator: {ex.Message}");
            return false;
        }
    }
}
