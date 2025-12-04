using CommunityToolkit.Mvvm.ComponentModel;
using ToLaunch.Models;
using ToLaunch.Services;

namespace ToLaunch.ViewModels;

public partial class ApplicationSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private bool closeToSystemTray;

    [ObservableProperty]
    private bool minimizeToSystemTray;

    [ObservableProperty]
    private bool startMainProgramWithStartAll;

    [ObservableProperty]
    private bool runAsAdministrator;

    /// <summary>
    /// Indicates if the application is currently running with administrator privileges.
    /// </summary>
    public bool IsRunningAsAdmin => AdminHelper.IsRunningAsAdministrator();

    /// <summary>
    /// Text to display for the Run as Administrator checkbox based on current elevation state.
    /// </summary>
    public string RunAsAdminText => IsRunningAsAdmin 
        ? "Run as Administrator (Currently elevated)" 
        : "Run as Administrator";

    /// <summary>
    /// Description text for the Run as Administrator setting.
    /// </summary>
    public string RunAsAdminDescription => IsRunningAsAdmin
        ? "ToLaunch is already running with administrator privileges."
        : "Restart ToLaunch with elevated privileges. A UAC prompt will appear.";

    public ApplicationSettingsViewModel()
    {
    }

    public ApplicationSettingsViewModel(bool startWithWindows, bool closeToSystemTray, bool minimizeToSystemTray, bool startMainProgramWithStartAll, bool runAsAdministrator)
    {
        StartWithWindows = startWithWindows;
        CloseToSystemTray = closeToSystemTray;
        MinimizeToSystemTray = minimizeToSystemTray;
        StartMainProgramWithStartAll = startMainProgramWithStartAll;
        RunAsAdministrator = runAsAdministrator;
    }

    public AppSettings ToModel()
    {
        return new AppSettings
        {
            StartWithWindows = StartWithWindows,
            CloseToSystemTray = CloseToSystemTray,
            MinimizeToSystemTray = MinimizeToSystemTray,
            StartMainProgramWithStartAll = StartMainProgramWithStartAll,
            RunAsAdministrator = RunAsAdministrator
        };
    }

    public void LoadFromModel(AppSettings model)
    {
        StartWithWindows = model.StartWithWindows;
        CloseToSystemTray = model.CloseToSystemTray;
        MinimizeToSystemTray = model.MinimizeToSystemTray;
        StartMainProgramWithStartAll = model.StartMainProgramWithStartAll;
        RunAsAdministrator = model.RunAsAdministrator;
    }
}
