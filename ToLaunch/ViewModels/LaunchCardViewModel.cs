using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToLaunch.Models;
using ToLaunch.Services;
using System.Diagnostics;
using System;
using Avalonia.Media.Imaging;
using System.IO;
using Material.Icons;
using System.Threading.Tasks;

namespace ToLaunch.ViewModels;
public partial class LaunchCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "New Program";

    [ObservableProperty]
    private string path = "";

    [ObservableProperty]
    private string arguments = "";

    [ObservableProperty]
    private string iconPath = "/Assets/ToLaunch.ico";

    [ObservableProperty]
    private Bitmap? iconBitmap;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private bool startHidden;

    [ObservableProperty]
    private bool stopOnExit = true;

    [ObservableProperty]
    private bool startWithProgram;

    [ObservableProperty]
    private string? startWithProgramName;

    [ObservableProperty]
    private bool stopWithProgram;

    [ObservableProperty]
    private string? stopWithProgramName;

    [ObservableProperty]
    private int delayStartSeconds;

    [ObservableProperty]
    private int delayStopSeconds;

    [ObservableProperty]
    private ProcessPriority priority = ProcessPriority.Default;

    [ObservableProperty]
    private long cpuAffinity = 0;

    private Process? _process;

    public MaterialIconKind IconKind => IsRunning ? MaterialIconKind.Stop : MaterialIconKind.Play;

    public Action<LaunchCardViewModel>? OnSettingsRequested { get; set; }
    public Action<LaunchCardViewModel>? OnDeleteRequested { get; set; }
    public Action? OnProgramChanged { get; set; }

    public LaunchCardViewModel()
    {
    }

    public LaunchCardViewModel(ProgramModel model)
    {
        LoadFromModel(model);
    }

    public void LoadFromModel(ProgramModel model)
    {
        Name = model.Name;
        Path = model.Path;
        Arguments = model.Arguments;
        IconPath = model.IconPath;
        LoadIconBitmap(model.IconPath);
        IsEnabled = model.IsEnabled;
        StartHidden = model.StartHidden;
        StopOnExit = model.StopOnExit;
        StartWithProgram = model.StartWithProgram;
        StartWithProgramName = model.StartWithProgramName;
        StopWithProgram = model.StopWithProgram;
        StopWithProgramName = model.StopWithProgramName;
        DelayStartSeconds = model.DelayStartSeconds;
        DelayStopSeconds = model.DelayStopSeconds;
        Priority = model.Priority;
        CpuAffinity = model.CpuAffinity;
    }

    public void LoadIconBitmap(string iconFilePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath))
            {
                IconBitmap = new Bitmap(iconFilePath);
            }
            else
            {
                // Load default cmd-icon.ico from assets
                var assets = Avalonia.Platform.AssetLoader.Open(new Uri("avares://ToLaunch/Assets/cmd-icon.ico"));
                IconBitmap = new Bitmap(assets);
            }
        }
        catch
        {
            try
            {
                // Fallback to default icon on error
                var assets = Avalonia.Platform.AssetLoader.Open(new Uri("avares://ToLaunch/Assets/cmd-icon.ico"));
                IconBitmap = new Bitmap(assets);
            }
            catch
            {
                IconBitmap = null;
            }
        }
        finally
        {
            OnPropertyChanged(nameof(IconBitmap));
        }
    }

    public ProgramModel ToModel()
    {
        return new ProgramModel
        {
            Name = Name,
            Path = Path,
            Arguments = Arguments,
            IconPath = IconPath,
            IsEnabled = IsEnabled,
            StartHidden = StartHidden,
            StopOnExit = StopOnExit,
            StartWithProgram = StartWithProgram,
            StartWithProgramName = StartWithProgramName,
            StopWithProgram = StopWithProgram,
            StopWithProgramName = StopWithProgramName,
            DelayStartSeconds = DelayStartSeconds,
            DelayStopSeconds = DelayStopSeconds,
            Priority = Priority,
            CpuAffinity = CpuAffinity
        };
    }

    [RelayCommand]
    private async Task Start()
    {
        if (IsRunning)
        {
            await StopProgramAsync();
        }
        else
        {
            await StartProgramAsync();
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        OnSettingsRequested?.Invoke(this);
    }

    /// <summary>
    /// Checks if the program is already running by looking for processes with matching name.
    /// </summary>
    private bool IsProgramAlreadyRunning()
    {
        if (string.IsNullOrWhiteSpace(Path))
            return false;

        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(Path);
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

    private async System.Threading.Tasks.Task StartProgramAsync()
    {
        if (string.IsNullOrWhiteSpace(Path) || !System.IO.File.Exists(Path))
            return;

        // Check if program is already running
        if (IsProgramAlreadyRunning())
        {
            IsRunning = true;
            return;
        }

        if (DelayStartSeconds > 0)
        {
            await System.Threading.Tasks.Task.Delay(DelayStartSeconds * 1000);
        }

        try
        {
            // If priority or affinity is set, we need to use UseShellExecute=false to get a proper process handle
            // Otherwise, use UseShellExecute=true for better compatibility
            bool needsProcessControl = Priority != ProcessPriority.Default || CpuAffinity != 0;

            var startInfo = new ProcessStartInfo
            {
                FileName = Path,
                Arguments = Arguments ?? string.Empty,
                UseShellExecute = !needsProcessControl,
                WindowStyle = StartHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            // When not using shell execute, we need to set working directory
            if (needsProcessControl)
            {
                startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(Path) ?? "";
            }

            _process = Process.Start(startInfo);
            IsRunning = true;

            if (_process != null)
            {
                // Apply process priority and affinity (with slight delay for launcher processes)
                if (needsProcessControl)
                {
                    _ = ApplyProcessSettingsAsync(_process);
                }

                // If starting hidden, use Windows API to aggressively hide the window
                // Many applications ignore ProcessWindowStyle.Hidden, so we need to force it
                if (StartHidden)
                {
                    // Fire and forget - don't block the UI while trying to hide windows
                    _ = WindowHider.TryHideProcessWindowsAsync(_process, maxAttempts: 10, delayBetweenAttempts: 300);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to start program {Name}", ex);
        }
    }

    /// <summary>
    /// Applies the configured process settings (priority and affinity) with a delay to handle launcher processes.
    /// </summary>
    private async Task ApplyProcessSettingsAsync(Process process)
    {
        // Wait a moment for launcher processes to spawn the actual application
        await Task.Delay(500);
        
        // Apply affinity first (usually works)
        ApplyProcessAffinity(process);
        
        // Apply priority (may need multiple attempts)
        await ApplyProcessPriorityWithRetryAsync(process);
    }

    /// <summary>
    /// Applies the configured process priority with retry logic.
    /// </summary>
    private async Task ApplyProcessPriorityWithRetryAsync(Process process)
    {
        if (Priority == ProcessPriority.Default)
            return;

        var targetPriority = Priority switch
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
            if (TrySetProcessPriority(process, targetPriority))
                return;

            // If that failed, try to find the process by name
            if (TrySetProcessPriorityByName(targetPriority))
                return;
        }

        LogService.LogError($"Failed to set priority for {Name} after multiple attempts");
    }

    /// <summary>
    /// Tries to set the priority on a specific process.
    /// </summary>
    private bool TrySetProcessPriority(Process process, ProcessPriorityClass targetPriority)
    {
        try
        {
            process.Refresh();
            
            if (process.HasExited)
                return false;

            // Get a fresh handle to the process
            using var freshProcess = Process.GetProcessById(process.Id);
            freshProcess.PriorityClass = targetPriority;
            LogService.LogInfo($"Set process priority for {Name} (PID: {process.Id}) to {Priority}");
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
            LogService.LogError($"Failed to set process priority for {Name}", ex);
            return false;
        }
    }

    /// <summary>
    /// Tries to set process priority by finding the process by name.
    /// </summary>
    private bool TrySetProcessPriorityByName(ProcessPriorityClass targetPriority)
    {
        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(Path);
            var processes = Process.GetProcessesByName(processName);
            bool anySuccess = false;

            foreach (var proc in processes)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.PriorityClass = targetPriority;
                        LogService.LogInfo($"Set process priority for {Name} (found by name, PID: {proc.Id}) to {Priority}");
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
            LogService.LogError($"Failed to find process by name for {Name}", ex);
            return false;
        }
    }

    /// <summary>
    /// Applies the configured process priority to the given process.
    /// </summary>
    private void ApplyProcessPriority(Process process)
    {
        if (Priority == ProcessPriority.Default)
            return;

        var targetPriority = Priority switch
        {
            ProcessPriority.Low => ProcessPriorityClass.BelowNormal,
            ProcessPriority.High => ProcessPriorityClass.AboveNormal,
            _ => ProcessPriorityClass.Normal
        };

        if (!TrySetProcessPriority(process, targetPriority))
        {
            TrySetProcessPriorityByName(targetPriority);
        }
    }

    /// <summary>
    /// Applies the configured CPU affinity to the given process.
    /// </summary>
    private void ApplyProcessAffinity(Process process)
    {
        if (CpuAffinity == 0)
            return; // 0 means use all cores (default)

        try
        {
            // First try to set affinity on the process we started
            if (!process.HasExited)
            {
                process.ProcessorAffinity = (IntPtr)CpuAffinity;
                LogService.LogInfo($"Set CPU affinity for {Name} to 0x{CpuAffinity:X}");
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // Process has exited, fall through to find by name
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to set CPU affinity for {Name}", ex);
        }

        // If the original process exited (e.g., it was a launcher), find the actual process by name
        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(Path);
            var processes = Process.GetProcessesByName(processName);
            foreach (var proc in processes)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.ProcessorAffinity = (IntPtr)CpuAffinity;
                        LogService.LogInfo($"Set CPU affinity for {Name} (found by name, PID: {proc.Id}) to 0x{CpuAffinity:X}");
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
            LogService.LogError($"Failed to find process by name for affinity {Name}", ex);
        }
    }

    private async System.Threading.Tasks.Task StopProgramAsync()
    {
        if (DelayStopSeconds > 0)
        {
            await System.Threading.Tasks.Task.Delay(DelayStopSeconds * 1000);
        }

        try
        {
            // First try to kill the tracked process if it exists and hasn't exited
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
                IsRunning = false;
                return;
            }

            // If no tracked process or it has exited, try to find and kill by process name
            if (!string.IsNullOrWhiteSpace(Path))
            {
                var processName = System.IO.Path.GetFileNameWithoutExtension(Path);
                var processes = Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Kill();
                        proc.Dispose();
                    }
                    catch
                    {
                        // Process may have already exited
                    }
                }
            }

            _process = null;
            IsRunning = false;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to stop program {Name}", ex);
        }
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnProgramChanged?.Invoke();
    }
}