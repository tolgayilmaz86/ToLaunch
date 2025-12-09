using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ToLaunch.Models;
using ToLaunch.ViewModels;

namespace ToLaunch.Services;
public class ProgramLaunchService
{
    private readonly Dictionary<string, Process> _runningProcesses = [];

    // P/Invoke declarations for setting process priority with proper access rights
    private const uint PROCESS_SET_INFORMATION = 0x0200;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetPriorityClass(IntPtr hProcess);

    // Priority class constants
    private const uint IDLE_PRIORITY_CLASS = 0x00000040;
    private const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    private const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    private const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    private const uint HIGH_PRIORITY_CLASS = 0x00000080;
    private const uint REALTIME_PRIORITY_CLASS = 0x00000100;

    public async Task StartProgramAsync(LaunchCardViewModel card)
    {
        if (string.IsNullOrWhiteSpace(card.Path) || !System.IO.File.Exists(card.Path))
            return;

        // Check if the program is already running
        if (IsProgramAlreadyRunning(card.Path))
        {
            return;
        }

        // Check if should start with another program
        if (card.StartWithProgram && !string.IsNullOrEmpty(card.StartWithProgramName))
        {
            // Wait for the dependent program to start
            // TODO: track program state
        }

        // Apply start delay
        if (card.DelayStartSeconds > 0)
        {
            await Task.Delay(card.DelayStartSeconds * 1000);
        }

        try
        {
            // If priority or affinity is set, we need to use UseShellExecute=false to get a proper process handle
            // Otherwise, use UseShellExecute=true for better compatibility
            bool needsProcessControl = card.Priority != ProcessPriority.Default || card.CpuAffinity != 0;

            var startInfo = new ProcessStartInfo
            {
                FileName = card.Path,
                Arguments = card.Arguments ?? string.Empty,
                UseShellExecute = !needsProcessControl,
                WindowStyle = card.StartHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            // When not using shell execute, we need to set working directory
            if (needsProcessControl)
            {
                startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(card.Path) ?? "";
            }

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _runningProcesses[card.Name] = process;
                LogService.LogInfo($"Started process {card.Name} (PID: {process.Id})");

                // Apply process settings (priority and affinity) with slight delay for launcher processes
                if (needsProcessControl)
                {
                    _ = ApplyProcessSettingsAsync(process, card.Path, card.Priority, card.CpuAffinity, card.Name);
                }

                // If starting hidden, use Windows API to aggressively hide the window
                if (card.StartHidden)
                {
                    _ = WindowHider.TryHideProcessWindowsAsync(process, maxAttempts: 10, delayBetweenAttempts: 300);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to start {card.Name}", ex);
        }
    }

    /// <summary>
    /// Applies the configured process settings (priority and affinity) with a delay to handle launcher processes.
    /// </summary>
    private static async Task ApplyProcessSettingsAsync(Process process, string programPath, ProcessPriority priority, long cpuAffinity, string programName)
    {
        // Wait a moment for launcher processes to spawn the actual application
        await Task.Delay(500);
        
        // Apply affinity first (usually works)
        ApplyProcessAffinity(process, programPath, cpuAffinity, programName);
        
        // Apply priority with retry logic
        await ApplyProcessPriorityWithRetryAsync(process, programPath, priority, programName);
    }

    /// <summary>
    /// Applies the configured process priority with retry logic.
    /// Some applications reset their priority after startup, so we retry multiple times over a longer period.
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

        // Try multiple times over a longer period (some apps reset priority after startup)
        // Attempts: 0ms, 500ms, 1500ms, 3000ms, 5000ms, 8000ms, 12000ms, 17000ms (~17 seconds total)
        int[] delays = [0, 500, 1000, 1500, 2000, 3000, 4000, 5000];
        
        for (int attempt = 0; attempt < delays.Length; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(delays[attempt]);

            // Try to set on the original process first
            bool success = TrySetAndVerifyProcessPriority(process, targetPriority, programName);
            
            // If that failed, try to find the process by name
            if (!success)
                success = TrySetAndVerifyProcessPriorityByName(programPath, targetPriority, programName);

            if (success && attempt > 0)
            {
                LogService.LogInfo($"Priority for {programName} set successfully on attempt {attempt + 1}");
            }
        }
    }

    /// <summary>
    /// Tries to set the priority on a specific process using native Windows API for proper access rights.
    /// </summary>
    private static bool TrySetProcessPriority(Process process, ProcessPriorityClass targetPriority, string programName)
    {
        try
        {
            process.Refresh();
            
            if (process.HasExited)
                return false;

            int processId = process.Id;
            return TrySetProcessPriorityById(processId, targetPriority, programName);
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process has exited
            return false;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set process priority for {programName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Tries to set and verify the priority on a specific process.
    /// Returns true only if the priority was set AND verified.
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
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set process priority for {programName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Tries to set and verify process priority by finding the process by name.
    /// </summary>
    private static bool TrySetAndVerifyProcessPriorityByName(string programPath, ProcessPriorityClass targetPriority, string programName)
    {
        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(programPath);
            var processes = Process.GetProcessesByName(processName);
            bool allSuccess = processes.Length > 0;

            foreach (var proc in processes)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        if (!TrySetAndVerifyProcessPriorityById(proc.Id, targetPriority, programName))
                        {
                            allSuccess = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Failed to set priority for PID {proc.Id}", ex);
                    allSuccess = false;
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to find process by name for {programName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Tries to set the priority and then verifies it was actually applied.
    /// </summary>
    private static bool TrySetAndVerifyProcessPriorityById(int processId, ProcessPriorityClass targetPriority, string programName)
    {
        IntPtr hProcess = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        
        if (hProcess == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            LogService.LogError($"Failed to open process {programName} (PID: {processId}) for priority change. Error code: {error}");
            return false;
        }

        try
        {
            uint targetPriorityClass = targetPriority switch
            {
                ProcessPriorityClass.Idle => IDLE_PRIORITY_CLASS,
                ProcessPriorityClass.BelowNormal => BELOW_NORMAL_PRIORITY_CLASS,
                ProcessPriorityClass.Normal => NORMAL_PRIORITY_CLASS,
                ProcessPriorityClass.AboveNormal => ABOVE_NORMAL_PRIORITY_CLASS,
                ProcessPriorityClass.High => HIGH_PRIORITY_CLASS,
                ProcessPriorityClass.RealTime => REALTIME_PRIORITY_CLASS,
                _ => NORMAL_PRIORITY_CLASS
            };

            // Set the priority
            if (!SetPriorityClass(hProcess, targetPriorityClass))
            {
                int error = Marshal.GetLastWin32Error();
                LogService.LogError($"SetPriorityClass failed for {programName} (PID: {processId}). Error code: {error}");
                return false;
            }

            // Verify the priority was actually set
            uint actualPriority = GetPriorityClass(hProcess);
            if (actualPriority == 0)
            {
                int error = Marshal.GetLastWin32Error();
                LogService.LogError($"GetPriorityClass failed for {programName} (PID: {processId}). Error code: {error}");
                return false;
            }

            if (actualPriority == targetPriorityClass)
            {
                LogService.LogInfo($"Set and verified process priority for {programName} (PID: {processId}) to {targetPriority}");
                return true;
            }
            else
            {
                string actualName = GetPriorityClassName(actualPriority);
                LogService.LogWarning($"Priority for {programName} (PID: {processId}) was set but changed back to {actualName}. App may be resetting its own priority.");
                return false;
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Gets a human-readable name for a priority class constant.
    /// </summary>
    private static string GetPriorityClassName(uint priorityClass)
    {
        return priorityClass switch
        {
            IDLE_PRIORITY_CLASS => "Idle",
            BELOW_NORMAL_PRIORITY_CLASS => "BelowNormal",
            NORMAL_PRIORITY_CLASS => "Normal",
            ABOVE_NORMAL_PRIORITY_CLASS => "AboveNormal",
            HIGH_PRIORITY_CLASS => "High",
            REALTIME_PRIORITY_CLASS => "RealTime",
            _ => $"Unknown (0x{priorityClass:X})"
        };
    }

    /// <summary>
    /// Tries to set the priority on a process by ID using native Windows API.
    /// </summary>
    private static bool TrySetProcessPriorityById(int processId, ProcessPriorityClass targetPriority, string programName)
    {
        // Open the process with SET_INFORMATION access right
        IntPtr hProcess = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        
        if (hProcess == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            LogService.LogError($"Failed to open process {programName} (PID: {processId}) for priority change. Error code: {error}");
            return false;
        }

        try
        {
            uint priorityClass = targetPriority switch
            {
                ProcessPriorityClass.Idle => IDLE_PRIORITY_CLASS,
                ProcessPriorityClass.BelowNormal => BELOW_NORMAL_PRIORITY_CLASS,
                ProcessPriorityClass.Normal => NORMAL_PRIORITY_CLASS,
                ProcessPriorityClass.AboveNormal => ABOVE_NORMAL_PRIORITY_CLASS,
                ProcessPriorityClass.High => HIGH_PRIORITY_CLASS,
                ProcessPriorityClass.RealTime => REALTIME_PRIORITY_CLASS,
                _ => NORMAL_PRIORITY_CLASS
            };

            if (SetPriorityClass(hProcess, priorityClass))
            {
                LogService.LogInfo($"Set process priority for {programName} (PID: {processId}) to {targetPriority}");
                return true;
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                LogService.LogError($"SetPriorityClass failed for {programName} (PID: {processId}). Error code: {error}");
                return false;
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Tries to set process priority by finding the process by name using native Windows API.
    /// </summary>
    private static bool TrySetProcessPriorityByName(string programPath, ProcessPriorityClass targetPriority, string programName)
    {
        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(programPath);
            var processes = Process.GetProcessesByName(processName);
            bool anySuccess = false;

            foreach (var proc in processes)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        if (TrySetProcessPriorityById(proc.Id, targetPriority, programName))
                        {
                            anySuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Failed to set priority for PID {proc.Id}", ex);
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return anySuccess;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to find process by name for {programName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Applies the configured CPU affinity to the given process.
    /// </summary>
    private static void ApplyProcessAffinity(Process process, string programPath, long cpuAffinity, string programName)
    {
        if (cpuAffinity == 0)
            return; // 0 means use all cores (default)

        try
        {
            // First try to set affinity on the process we started
            if (!process.HasExited)
            {
                process.ProcessorAffinity = (IntPtr)cpuAffinity;
                LogService.LogInfo($"Set CPU affinity for {programName} to 0x{cpuAffinity:X}");
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // Process has exited, fall through to find by name
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set CPU affinity for {programName}", ex);
        }

        // If the original process exited (e.g., it was a launcher), find the actual process by name
        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(programPath);
            var processes = Process.GetProcessesByName(processName);
            foreach (var proc in processes)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.ProcessorAffinity = (IntPtr)cpuAffinity;
                        LogService.LogInfo($"Set CPU affinity for {programName} (found by name, PID: {proc.Id}) to 0x{cpuAffinity:X}");
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Failed to set affinity for PID {proc.Id}", ex);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to find process by name for affinity {programName}", ex);
        }
    }

    public async Task StopProgramAsync(LaunchCardViewModel card)
    {
        // Apply stop delay
        if (card.DelayStopSeconds > 0)
        {
            await Task.Delay(card.DelayStopSeconds * 1000);
        }

        if (_runningProcesses.TryGetValue(card.Name, out var process))
        {
            try
            {
                process.Kill();
                process.Dispose();
                _runningProcesses.Remove(card.Name);
                LogService.LogInfo($"Stopped process {card.Name}");
            }
            catch (Exception ex)
            {
                LogService.LogError($"Failed to stop {card.Name}", ex);
            }
        }
    }

    public bool IsProgramRunning(string programName)
    {
        return _runningProcesses.ContainsKey(programName) &&
               !_runningProcesses[programName].HasExited;
    }

    /// <summary>
    /// Checks if a program is already running by looking for processes with matching name.
    /// </summary>
    private static bool IsProgramAlreadyRunning(string programPath)
    {
        if (string.IsNullOrWhiteSpace(programPath))
            return false;

        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(programPath);
            var processes = Process.GetProcessesByName(processName);
            var isRunning = processes.Length > 0;

            // Dispose the process objects to avoid resource leaks
            foreach (var proc in processes)
            {
                proc.Dispose();
            }

            return isRunning;
        }
        catch
        {
            return false;
        }
    }
}