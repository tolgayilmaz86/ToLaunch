using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToLaunch.Models;
using ToLaunch.Services;
using System;
using Avalonia.Media.Imaging;
using System.IO;
using Material.Icons;
using System.Threading.Tasks;
using Avalonia.Media;
using System.Collections.Generic;
using System.Diagnostics;

namespace ToLaunch.ViewModels;
public partial class LaunchCardViewModel : ObservableObject
{
    private readonly ProgramLaunchService _launchService = new();

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

    public MaterialIconKind IconKind => IsRunning ? MaterialIconKind.Stop : MaterialIconKind.Play;

    public IBrush ButtonBackground => IsRunning ? new SolidColorBrush(Colors.LightCoral) : new SolidColorBrush(Colors.LightBlue);

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

        UpdateRunningStatus();
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
        // Check both the IsRunning flag AND if the process is actually running
        bool actuallyRunning = !string.IsNullOrWhiteSpace(Path) && 
                               ProgramLaunchService.IsProgramAlreadyRunning(Path);
        
        if (IsRunning || actuallyRunning)
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

    private async Task StartProgramAsync()
    {
        if (string.IsNullOrWhiteSpace(Path) || !File.Exists(Path))
            return;

        // Check if program is already running
        if (_launchService.IsProgramRunning(Name))
        {
            IsRunning = true;
            return;
        }

        await _launchService.StartProgramAsync(this);
        IsRunning = true;
    }

    private async Task StopProgramAsync()
    {
        await _launchService.StopProgramAsync(this);
        IsRunning = false;
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(ButtonBackground));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnProgramChanged?.Invoke();
    }

    public void UpdateRunningStatus()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            IsRunning = false;
            return;
        }

        // Check if the program is actually running (either started by us or externally)
        bool isActuallyRunning = ProgramLaunchService.IsProgramAlreadyRunning(Path);
        IsRunning = isActuallyRunning;
    }

    public void UpdateRunningStatus(Process[] allProcesses)
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            IsRunning = false;
            return;
        }

        // Check if the program is actually running (either started by us or externally)
        bool isActuallyRunning = ProgramLaunchService.IsProgramAlreadyRunning(Path, allProcesses);
        IsRunning = isActuallyRunning;
    }
}