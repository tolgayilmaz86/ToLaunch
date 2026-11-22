using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ToLaunch.Services;

/// <summary>
/// Service for managing window visibility using Windows API.
/// This provides more control over window hiding than ProcessWindowStyle alone.
/// </summary>
public static class WindowHider
{
    private const int SW_HIDE = 0;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// Attempts to hide the main window of a process, retrying until one appears.
    /// This is more aggressive than ProcessWindowStyle.Hidden.
    /// </summary>
    /// <param name="process">The process whose window should be hidden</param>
    /// <param name="maxAttempts">Maximum number of attempts (some apps create windows after startup)</param>
    /// <param name="delayBetweenAttempts">Delay between attempts in milliseconds</param>
    public static async Task TryHideProcessWindowsAsync(Process? process, int maxAttempts = 5, int delayBetweenAttempts = 500)
    {
        if (process == null || process.HasExited)
            return;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                // Wait a bit for the window to be created
                await Task.Delay(delayBetweenAttempts);

                if (process.HasExited)
                    return;

                // Refresh to get the latest window handle
                process.Refresh();

                // Keep waiting until the process actually has a window to hide.
                if (process.MainWindowHandle == IntPtr.Zero)
                    continue;

                // ShowWindow's result is the window's previous visibility, not a success flag, so it is
                // not worth testing: a non-zero handle means the window exists and has been acted on.
                ShowWindow(process.MainWindowHandle, SW_HIDE);
                LogService.LogInfo($"Hidden window for process {process.ProcessName} (attempt {attempt + 1})");

                // Stop here; retrying would also re-hide a window the user deliberately restored.
                return;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error hiding window (attempt {attempt + 1}): {ex.Message}");
            }
        }
    }
}
