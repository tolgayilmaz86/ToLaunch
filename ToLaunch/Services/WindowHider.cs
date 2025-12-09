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
    private const int SW_MINIMIZE = 6;
    private const int SW_SHOWMINNOACTIVE = 7;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Attempts to hide all windows belonging to a process.
    /// This is more aggressive than ProcessWindowStyle.Hidden.
    /// </summary>
    /// <param name="process">The process whose windows should be hidden</param>
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

                // Try to hide the main window
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, SW_HIDE);
                    LogService.LogInfo($"Hidden window for process {process.ProcessName} (attempt {attempt + 1})");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error hiding window (attempt {attempt + 1}): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Attempts to minimize all windows belonging to a process.
    /// This is an alternative to hiding that more applications respect.
    /// </summary>
    /// <param name="process">The process whose windows should be minimized</param>
    public static async Task TryMinimizeProcessWindowsAsync(Process? process, int maxAttempts = 3, int delayBetweenAttempts = 500)
    {
        if (process == null || process.HasExited)
            return;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await Task.Delay(delayBetweenAttempts);

                if (process.HasExited)
                    return;

                process.Refresh();

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, SW_SHOWMINNOACTIVE);
                    LogService.LogInfo($"Minimized window for process {process.ProcessName} (attempt {attempt + 1})");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error minimizing window (attempt {attempt + 1}): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Hides a specific window handle.
    /// </summary>
    public static void HideWindow(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_HIDE);
        }
    }

    /// <summary>
    /// Minimizes a specific window handle without activating it.
    /// </summary>
    public static void MinimizeWindow(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_SHOWMINNOACTIVE);
        }
    }
}
