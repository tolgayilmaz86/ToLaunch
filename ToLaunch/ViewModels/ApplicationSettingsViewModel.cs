using CommunityToolkit.Mvvm.ComponentModel;
using ToLaunch.Models;
using ToLaunch.Services;

namespace ToLaunch.ViewModels;

public partial class ApplicationSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private bool startMinimized;

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

    public AppSettings ToModel()
    {
        return new AppSettings
        {
            StartWithWindows = StartWithWindows,
            StartMinimized = StartMinimized,
            CloseToSystemTray = CloseToSystemTray,
            MinimizeToSystemTray = MinimizeToSystemTray,
            StartMainProgramWithStartAll = StartMainProgramWithStartAll,
            RunAsAdministrator = RunAsAdministrator
        };
    }

    public void LoadFromModel(AppSettings model)
    {
        StartWithWindows = model.StartWithWindows;
        StartMinimized = model.StartMinimized;
        CloseToSystemTray = model.CloseToSystemTray;
        MinimizeToSystemTray = model.MinimizeToSystemTray;
        StartMainProgramWithStartAll = model.StartMainProgramWithStartAll;
        RunAsAdministrator = model.RunAsAdministrator;
    }
}
