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
            DelayStopSeconds = DelayStopSeconds
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

    private async System.Threading.Tasks.Task StartProgramAsync()
    {
        if (string.IsNullOrWhiteSpace(Path) || !System.IO.File.Exists(Path))
            return;

        if (DelayStartSeconds > 0)
        {
            await System.Threading.Tasks.Task.Delay(DelayStartSeconds * 1000);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path,
                Arguments = Arguments ?? string.Empty,
                UseShellExecute = true,
                WindowStyle = StartHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            _process = Process.Start(startInfo);
            IsRunning = true;

            // If starting hidden, use Windows API to aggressively hide the window
            // Many applications ignore ProcessWindowStyle.Hidden, so we need to force it
            if (StartHidden && _process != null)
            {
                // Fire and forget - don't block the UI while trying to hide windows
                _ = WindowHider.TryHideProcessWindowsAsync(_process, maxAttempts: 10, delayBetweenAttempts: 300);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start program: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to stop program: {ex.Message}");
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