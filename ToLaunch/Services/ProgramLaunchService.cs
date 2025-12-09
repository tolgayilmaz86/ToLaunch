using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ToLaunch.Models;
using ToLaunch.ViewModels;

namespace ToLaunch.Services;
public class ProgramLaunchService
{
    private readonly Dictionary<string, Process> _runningProcesses = [];

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

        // Try multiple times with increasing delays
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(500);

            // Try to set on the original process first
            if (TrySetProcessPriority(process, targetPriority, programName))
                return;

            // If that failed, try to find the process by name
            if (TrySetProcessPriorityByName(programPath, targetPriority, programName))
                return;
        }

        LogService.LogError($"Failed to set priority for {programName} after multiple attempts");
    }

    /// <summary>
    /// Tries to set the priority on a specific process.
    /// </summary>
    private static bool TrySetProcessPriority(Process process, ProcessPriorityClass targetPriority, string programName)
    {
        try
        {
            process.Refresh();
            
            if (process.HasExited)
                return false;

            // Get a fresh handle to the process
            using var freshProcess = Process.GetProcessById(process.Id);
            freshProcess.PriorityClass = targetPriority;
            LogService.LogInfo($"Set process priority for {programName} (PID: {process.Id}) to {targetPriority}");
            return true;
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
    /// Tries to set process priority by finding the process by name.
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
                        proc.PriorityClass = targetPriority;
                        LogService.LogInfo($"Set process priority for {programName} (found by name, PID: {proc.Id}) to {targetPriority}");
                        anySuccess = true;
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
    /// Applies the configured process priority to the given process.
    /// </summary>
    private static void ApplyProcessPriority(Process process, string programPath, ProcessPriority priority, string programName)
    {
        if (priority == ProcessPriority.Default)
            return;

        var targetPriority = priority switch
        {
            ProcessPriority.Low => ProcessPriorityClass.BelowNormal,
            ProcessPriority.High => ProcessPriorityClass.AboveNormal,
            _ => ProcessPriorityClass.Normal
        };

        if (!TrySetProcessPriority(process, targetPriority, programName))
        {
            TrySetProcessPriorityByName(programPath, targetPriority, programName);
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