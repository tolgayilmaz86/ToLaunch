using CommunityToolkit.Mvvm.ComponentModel;
using ToLaunch.Models;

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

    public ApplicationSettingsViewModel()
    {
    }

    public ApplicationSettingsViewModel(bool startWithWindows, bool closeToSystemTray, bool minimizeToSystemTray, bool startMainProgramWithStartAll)
    {
        StartWithWindows = startWithWindows;
        CloseToSystemTray = closeToSystemTray;
        MinimizeToSystemTray = minimizeToSystemTray;
        StartMainProgramWithStartAll = startMainProgramWithStartAll;
    }

    public AppSettings ToModel()
    {
        return new AppSettings
        {
            StartWithWindows = StartWithWindows,
            CloseToSystemTray = CloseToSystemTray,
            MinimizeToSystemTray = MinimizeToSystemTray,
            StartMainProgramWithStartAll = StartMainProgramWithStartAll
        };
    }

    public void LoadFromModel(AppSettings model)
    {
        StartWithWindows = model.StartWithWindows;
        CloseToSystemTray = model.CloseToSystemTray;
        MinimizeToSystemTray = model.MinimizeToSystemTray;
        StartMainProgramWithStartAll = model.StartMainProgramWithStartAll;
    }
}
