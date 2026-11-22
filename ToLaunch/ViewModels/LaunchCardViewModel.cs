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
using System.Diagnostics;

namespace ToLaunch.ViewModels;
public partial class LaunchCardViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Extra time beyond the configured start delay during which a program is still considered
    /// "starting". Prevents the process monitor from contradicting a launch that has not appeared in
    /// the process table yet, which would otherwise flip the button back and re-trigger the launch.
    /// </summary>
    private static readonly TimeSpan LaunchGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>The process this card started, if it is still alive and owned by us.</summary>
    private Process? _startedProcess;

    private DateTime _launchPendingUntil = DateTime.MinValue;

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

    /// <summary>
    /// The card's running state as of the previous monitor tick, used to detect the transitions that
    /// drive the start-with / stop-with dependencies.
    /// </summary>
    internal bool PreviousIsRunning { get; set; }

    /// <summary>True while a start is in flight and the process has not been observed yet.</summary>
    internal bool IsLaunchPending => DateTime.UtcNow < _launchPendingUntil;

    public Action<LaunchCardViewModel>? OnSettingsRequested { get; set; }
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
                IconBitmap = LoadDefaultIcon();
            }
        }
        catch
        {
            IconBitmap = LoadDefaultIcon();
        }
    }

    private static Bitmap? LoadDefaultIcon()
    {
        try
        {
            using var asset = Avalonia.Platform.AssetLoader.Open(new Uri("avares://ToLaunch/Assets/cmd-icon.ico"));
            return new Bitmap(asset);
        }
        catch
        {
            return null;
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

    /// <summary>
    /// The card's play/stop button: stops the program when it is running, starts it otherwise.
    /// </summary>
    [RelayCommand]
    private async Task Start()
    {
        if (IsRunning)
            await StopAsync();
        else
            await StartAsync();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        OnSettingsRequested?.Invoke(this);
    }

    /// <summary>
    /// Starts the program. Does nothing if it is already running or a start is already in flight.
    /// <see cref="IsRunning"/> is only set when the launch actually succeeded.
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning || IsLaunchPending)
            return;

        // Hold the "starting" state across the configured delay plus a grace period, so the monitor
        // does not contradict us before the process shows up in the process table.
        _launchPendingUntil = DateTime.UtcNow + TimeSpan.FromSeconds(DelayStartSeconds) + LaunchGracePeriod;

        try
        {
            var process = await ProgramLaunchService.StartProgramAsync(this);

            if (process == null)
            {
                // Launch failed; drop the pending window so the button returns to Play immediately.
                _launchPendingUntil = DateTime.MinValue;
                IsRunning = false;
                return;
            }

            ReplaceStartedProcess(process);
            IsRunning = true;
        }
        catch (Exception ex)
        {
            _launchPendingUntil = DateTime.MinValue;
            IsRunning = false;
            LogService.LogError($"Failed to start {Name}", ex);
        }
    }

    /// <summary>Stops the program and clears any pending-launch state.</summary>
    public async Task StopAsync()
    {
        _launchPendingUntil = DateTime.MinValue;

        try
        {
            await ProgramLaunchService.StopProgramAsync(this, _startedProcess);
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to stop {Name}", ex);
        }
        finally
        {
            ReplaceStartedProcess(null);
            IsRunning = false;
        }
    }

    /// <summary>
    /// Updates <see cref="IsRunning"/> from a process snapshot. A snapshot that does not see the program
    /// is ignored while a launch is still pending.
    /// </summary>
    internal void RefreshRunningState(ProcessSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            IsRunning = false;
            return;
        }

        if (snapshot.IsRunning(Path))
        {
            // Observed running: the launch (if any) has landed.
            _launchPendingUntil = DateTime.MinValue;
            IsRunning = true;
            return;
        }

        if (IsLaunchPending)
            return;

        if (IsRunning)
        {
            // The program is gone; release the handle we were holding.
            ReplaceStartedProcess(null);
        }

        IsRunning = false;
    }

    private void ReplaceStartedProcess(Process? process)
    {
        _startedProcess?.Dispose();
        _startedProcess = process;
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

    public void Dispose()
    {
        ReplaceStartedProcess(null);
    }
}
