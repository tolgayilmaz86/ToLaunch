using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ToLaunch.ViewModels;

namespace ToLaunch.Services;
public class ProgramLaunchService
{
    private readonly Dictionary<string, Process> _runningProcesses = [];

    public async Task StartProgramAsync(LaunchCardViewModel card)
    {
        if (string.IsNullOrWhiteSpace(card.Path) || !System.IO.File.Exists(card.Path))
            return;

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
            var startInfo = new ProcessStartInfo
            {
                FileName = card.Path,
                Arguments = card.Arguments ?? string.Empty,
                UseShellExecute = true,
                WindowStyle = card.StartHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _runningProcesses[card.Name] = process;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start {card.Name}: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to stop {card.Name}: {ex.Message}");
            }
        }
    }

    public bool IsProgramRunning(string programName)
    {
        return _runningProcesses.ContainsKey(programName) &&
               !_runningProcesses[programName].HasExited;
    }
}