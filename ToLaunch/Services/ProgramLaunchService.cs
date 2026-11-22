using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ToLaunch.Models;
using ToLaunch.ViewModels;

namespace ToLaunch.Services;

/// <summary>
/// Launches and terminates the programs configured on a launch card.
/// </summary>
/// <remarks>
/// Stateless by design. Process ownership lives on the card that started the program, and discovery of
/// externally started programs goes through <see cref="ProcessSnapshot"/>, which matches on the full
/// executable path rather than the file name alone.
/// </remarks>
public static class ProgramLaunchService
{
    /// <summary>
    /// Starts the program configured on <paramref name="card"/>, honouring its start delay, hidden-start,
    /// priority and CPU affinity settings.
    /// </summary>
    /// <returns>
    /// The started process, or <c>null</c> when the program could not be started. A launcher-style
    /// executable may exit immediately after spawning the real application, so a non-null result means
    /// the launch succeeded, not that this particular process is still alive.
    /// </returns>
    public static async Task<Process?> StartProgramAsync(LaunchCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (string.IsNullOrWhiteSpace(card.Path) || !System.IO.File.Exists(card.Path))
        {
            LogService.LogWarning($"Cannot start {card.Name}: '{card.Path}' does not exist");
            return null;
        }

        if (card.DelayStartSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(card.DelayStartSeconds));
        }

        try
        {
            // Priority and affinity need a real process handle, which requires UseShellExecute=false.
            // Otherwise shell execution is preferred for compatibility.
            bool needsProcessControl = card.Priority != ProcessPriority.Default || card.CpuAffinity != 0;

            var startInfo = new ProcessStartInfo
            {
                FileName = card.Path,
                Arguments = card.Arguments ?? string.Empty,
                UseShellExecute = !needsProcessControl,
                WindowStyle = card.StartHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            if (needsProcessControl)
            {
                startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(card.Path) ?? string.Empty;
            }

            var process = Process.Start(startInfo);
            if (process == null)
            {
                LogService.LogWarning($"Process.Start returned no process for {card.Name}");
                return null;
            }

            LogService.LogInfo($"Started process {card.Name} (PID: {process.Id})");

            if (needsProcessControl)
            {
                _ = ApplyProcessSettingsAsync(process, card.Path, card.Priority, card.CpuAffinity, card.Name);
            }

            if (card.StartHidden)
            {
                _ = WindowHider.TryHideProcessWindowsAsync(process, maxAttempts: 10, delayBetweenAttempts: 300);
            }

            return process;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to start {card.Name}", ex);
            return null;
        }
    }

    /// <summary>
    /// Stops the program configured on <paramref name="card"/>, honouring its stop delay.
    /// </summary>
    /// <param name="trackedProcess">
    /// The process this card started, if still held. Killed first, since it is the one process we know
    /// belongs to this card.
    /// </param>
    /// <returns>True when at least one process was terminated.</returns>
    public static async Task<bool> StopProgramAsync(LaunchCardViewModel card, Process? trackedProcess)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.DelayStopSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(card.DelayStopSeconds));
        }

        bool stopped = TryKill(trackedProcess, card.Name, "tracked");

        // Also stop matching processes that we did not start ourselves (or that a launcher spawned).
        // Matching is by full path, so a same-named program from another folder is left alone.
        if (!string.IsNullOrWhiteSpace(card.Path))
        {
            try
            {
                using var snapshot = ProcessSnapshot.Capture([card.Path]);
                foreach (var process in snapshot.Find(card.Path))
                {
                    stopped |= TryKill(process, card.Name, "matched by path");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"Failed to look up processes to stop for {card.Name}", ex);
            }
        }

        if (!stopped)
        {
            LogService.LogWarning($"No running process found to stop for {card.Name}");
        }

        return stopped;
    }

    private static bool TryKill(Process? process, string programName, string origin)
    {
        if (process == null)
            return false;

        try
        {
            if (process.HasExited)
                return false;

            int processId = process.Id;
            process.Kill();
            LogService.LogInfo($"Stopped {programName} ({origin}, PID: {processId})");
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to stop {programName} ({origin})", ex);
            return false;
        }
    }

    /// <summary>
    /// Applies priority and affinity after a short delay, giving launcher processes time to spawn the
    /// application they front for.
    /// </summary>
    private static async Task ApplyProcessSettingsAsync(Process process, string programPath, ProcessPriority priority, long cpuAffinity, string programName)
    {
        await Task.Delay(500);

        // Affinity usually succeeds straight away; priority may need retrying.
        ApplyProcessAffinity(process, programPath, cpuAffinity, programName);
        await ApplyProcessPriorityWithRetryAsync(process, programPath, priority, programName);
    }

    /// <summary>
    /// Applies the configured process priority, retrying because some applications reset their own
    /// priority shortly after startup. Stops as soon as the priority is verified as applied.
    /// </summary>
    private static async Task ApplyProcessPriorityWithRetryAsync(Process process, string programPath, ProcessPriority priority, string programName)
    {
        if (priority == ProcessPriority.Default)
            return;

        var targetPriority = priority switch
        {
            ProcessPriority.Low => ProcessPriorityClass.BelowNormal,
            ProcessPriority.High => ProcessPriorityClass.AboveNormal,
            _ => ProcessPriorityClass.Normal
        };

        // Waits before each attempt after the first; ~17s in total if every attempt fails.
        int[] delaysMs = [0, 500, 1000, 1500, 2000, 3000, 4000, 5000];

        for (int attempt = 0; attempt < delaysMs.Length; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(delaysMs[attempt]);

            // Try the process we started, then any process matching the configured path.
            bool success = TrySetAndVerifyProcessPriority(process, targetPriority, programName)
                        || TrySetAndVerifyProcessPriorityByPath(programPath, targetPriority, programName);

            if (success)
            {
                if (attempt > 0)
                    LogService.LogInfo($"Priority for {programName} set successfully on attempt {attempt + 1}");
                return;
            }
        }

        LogService.LogWarning($"Gave up setting priority for {programName} after {delaysMs.Length} attempts");
    }

    /// <summary>
    /// Sets and verifies the priority on a specific process.
    /// Returns true only if the priority was set AND read back as the target value.
    /// </summary>
    private static bool TrySetAndVerifyProcessPriority(Process process, ProcessPriorityClass targetPriority, string programName)
    {
        try
        {
            process.Refresh();

            if (process.HasExited)
                return false;

            return TrySetAndVerifyProcessPriorityById(process.Id, targetPriority, programName);
        }
        catch (ArgumentException)
        {
            // Process no longer exists.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process has exited.
            return false;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set process priority for {programName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Sets and verifies the priority on every process matching <paramref name="programPath"/>.
    /// Used when the process we started was a launcher that has already exited.
    /// </summary>
    private static bool TrySetAndVerifyProcessPriorityByPath(string programPath, ProcessPriorityClass targetPriority, string programName)
    {
        try
        {
            using var snapshot = ProcessSnapshot.Capture([programPath]);
            var processes = snapshot.Find(programPath);

            if (processes.Count == 0)
                return false;

            bool allSuccess = true;
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited && !TrySetAndVerifyProcessPriorityById(process.Id, targetPriority, programName))
                        allSuccess = false;
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Failed to set priority for {programName}", ex);
                    allSuccess = false;
                }
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to find processes by path for {programName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Sets the priority via the native API (so the handle carries the right access rights), then reads
    /// it back to confirm the application did not immediately override it.
    /// </summary>
    private static bool TrySetAndVerifyProcessPriorityById(int processId, ProcessPriorityClass targetPriority, string programName)
    {
        IntPtr hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_SET_INFORMATION | NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId);

        if (hProcess == IntPtr.Zero)
        {
            LogService.LogWarning($"Could not open process {programName} (PID: {processId}) to change priority. Error code: {Marshal.GetLastWin32Error()}");
            return false;
        }

        try
        {
            uint targetPriorityClass = ToPriorityClass(targetPriority);

            if (!NativeMethods.SetPriorityClass(hProcess, targetPriorityClass))
            {
                LogService.LogWarning($"SetPriorityClass failed for {programName} (PID: {processId}). Error code: {Marshal.GetLastWin32Error()}");
                return false;
            }

            uint actualPriority = NativeMethods.GetPriorityClass(hProcess);
            if (actualPriority == 0)
            {
                LogService.LogWarning($"GetPriorityClass failed for {programName} (PID: {processId}). Error code: {Marshal.GetLastWin32Error()}");
                return false;
            }

            if (actualPriority == targetPriorityClass)
            {
                LogService.LogInfo($"Set and verified process priority for {programName} (PID: {processId}) to {targetPriority}");
                return true;
            }

            LogService.LogWarning($"Priority for {programName} (PID: {processId}) was set but changed back to {GetPriorityClassName(actualPriority)}. App may be resetting its own priority.");
            return false;
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }

    private static uint ToPriorityClass(ProcessPriorityClass priority) => priority switch
    {
        ProcessPriorityClass.Idle => NativeMethods.IDLE_PRIORITY_CLASS,
        ProcessPriorityClass.BelowNormal => NativeMethods.BELOW_NORMAL_PRIORITY_CLASS,
        ProcessPriorityClass.Normal => NativeMethods.NORMAL_PRIORITY_CLASS,
        ProcessPriorityClass.AboveNormal => NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS,
        ProcessPriorityClass.High => NativeMethods.HIGH_PRIORITY_CLASS,
        ProcessPriorityClass.RealTime => NativeMethods.REALTIME_PRIORITY_CLASS,
        _ => NativeMethods.NORMAL_PRIORITY_CLASS
    };

    private static string GetPriorityClassName(uint priorityClass) => priorityClass switch
    {
        NativeMethods.IDLE_PRIORITY_CLASS => "Idle",
        NativeMethods.BELOW_NORMAL_PRIORITY_CLASS => "BelowNormal",
        NativeMethods.NORMAL_PRIORITY_CLASS => "Normal",
        NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS => "AboveNormal",
        NativeMethods.HIGH_PRIORITY_CLASS => "High",
        NativeMethods.REALTIME_PRIORITY_CLASS => "RealTime",
        _ => $"Unknown (0x{priorityClass:X})"
    };

    /// <summary>
    /// Applies the configured CPU affinity, falling back to processes matching the configured path when
    /// the process we started has already exited.
    /// </summary>
    private static void ApplyProcessAffinity(Process process, string programPath, long cpuAffinity, string programName)
    {
        if (cpuAffinity == 0)
            return; // 0 means use all cores (default)

        try
        {
            if (!process.HasExited)
            {
                process.ProcessorAffinity = (IntPtr)cpuAffinity;
                LogService.LogInfo($"Set CPU affinity for {programName} to 0x{cpuAffinity:X}");
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // Process has exited; fall through to matching by path.
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set CPU affinity for {programName}", ex);
        }

        try
        {
            using var snapshot = ProcessSnapshot.Capture([programPath]);
            foreach (var match in snapshot.Find(programPath))
            {
                try
                {
                    if (!match.HasExited)
                    {
                        match.ProcessorAffinity = (IntPtr)cpuAffinity;
                        LogService.LogInfo($"Set CPU affinity for {programName} (PID: {match.Id}) to 0x{cpuAffinity:X}");
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Failed to set affinity for {programName}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to find processes by path for affinity {programName}", ex);
        }
    }
}
