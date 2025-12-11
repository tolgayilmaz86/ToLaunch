using Avalonia.Controls;
using ToLaunch.ViewModels;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using ToLaunch.Services;
using System.Runtime.InteropServices;

namespace ToLaunch.Views;
public partial class MainWindow : Window
{
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    private bool _forceClose = false;

    // Win32 interop for removing minimize button
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // If force close is set, allow the window to close
        if (_forceClose)
            return;

        // Check if CloseToSystemTray is enabled
        if (ViewModel?.AppSettings.CloseToSystemTray == true)
        {
            e.Cancel = true;
            Hide();
        }
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Handle MinimizeToSystemTray
        if (change.Property == WindowStateProperty)
        {
            var newState = (WindowState?)change.NewValue;
            if (newState == WindowState.Minimized && ViewModel?.AppSettings.MinimizeToSystemTray == true)
            {
                Hide();
            }
        }
    }

    /// <summary>
    /// Forces the window to close, bypassing the system tray setting.
    /// Called when user clicks "Exit" from the tray menu.
    /// </summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel != null)
        {
            ViewModel.OnAddProgramRequested = ShowAddProgramDialog;
            ViewModel.OnEditProgramRequested = ShowEditProgramDialog;
            ViewModel.OnApplicationSettingsRequested = ShowApplicationSettingsDialog;
            ViewModel.OnSaveProfileDialogRequested = ShowSaveProfileDialog;
            ViewModel.OnLoadProfileDialogRequested = ShowLoadProfileDialog;
            ViewModel.OnSelectMainProgramRequested = ShowSelectMainProgramDialog;

            // Wire up callbacks for existing cards (cards that were loaded from profile)
            foreach (var card in ViewModel.LaunchCards)
            {
                card.OnSettingsRequested = ShowEditProgramDialog;
                card.OnProgramChanged = () => ViewModel.SaveCurrentProfile();
            }
        }
    }

    private async void ShowAddProgramDialog()
    {
        var viewModel = new LaunchCardSettingsViewModel
        {
            IsNewProgram = true
        };

        if (ViewModel != null)
        {
            viewModel.LoadAvailablePrograms(ViewModel.LaunchCards);
        }

        var dialog = new Window
        {
            Title = "Add Program",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new LaunchCardSettingsView
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowDialog(this);

        if (viewModel.SaveRequested && ViewModel != null)
        {
            var result = viewModel.ToModel();
            ViewModel.AddProgramFromModel(result);

            // Auto-save profile after adding program
            ViewModel.SaveCurrentProfile();
            LogService.LogError($"Profile auto-saved after adding program: {result.Name}");
        }
    }

    private async void ShowEditProgramDialog(LaunchCardViewModel card)
    {
        var viewModel = new LaunchCardSettingsViewModel(card.ToModel(), ViewModel?.LaunchCards ?? new())
        {
            IsNewProgram = false
        };

        var dialog = new Window
        {
            Title = "Edit Program",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new LaunchCardSettingsView
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowDialog(this);

        if (viewModel.DeleteRequested && ViewModel != null)
        {
            ViewModel.DeleteProgram(card);

            // Auto-save profile after deleting program
            ViewModel.SaveCurrentProfile();
            LogService.LogInfo($"Profile auto-saved after deleting program: {card.Name}");
        }
        else if (viewModel.SaveRequested)
        {
            var result = viewModel.ToModel();
            card.LoadFromModel(result);

            // Auto-save profile after editing program
            if (ViewModel != null)
            {
                ViewModel.SaveCurrentProfile();
                LogService.LogInfo($"Profile auto-saved after editing program: {result.Name}");
            }
        }
    }

    private async void ShowApplicationSettingsDialog()
    {
        var viewModel = new ApplicationSettingsViewModel();
        var wasRunAsAdmin = ViewModel?.AppSettings.RunAsAdministrator ?? false;

        if (ViewModel != null)
        {
            viewModel.LoadFromModel(ViewModel.AppSettings);
        }

        var dialog = new Window
        {
            Title = "Application Settings",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            MinWidth = 0,
            MinHeight = 0,
            Content = new ApplicationSettingsView
            {
                DataContext = viewModel
            }
        };

        // Remove minimize button (Windows-specific via Win32 interop)
        dialog.Opened += (s, e) =>
        {
            var platformHandle = dialog.TryGetPlatformHandle();
            if (platformHandle != null)
            {
                var hwnd = platformHandle.Handle;
                const int GWL_STYLE = -16;
                const int WS_MINIMIZEBOX = 0x20000;
                var style = GetWindowLong(hwnd, GWL_STYLE);
                var minimizedStyle = style & ~WS_MINIMIZEBOX;
                var result = SetWindowLong(hwnd, GWL_STYLE, minimizedStyle);
                // If SetWindowLong fails, it returns 0. Optionally, log or handle the error.
                if (result == 0)
                {
                    LogService.LogWarning("Failed to update window style to remove minimize button.");
                }
            }
        };

        await dialog.ShowDialog(this);

        // Save settings when dialog closes
        if (ViewModel != null)
        {
            var result = viewModel.ToModel();
            ViewModel.AppSettings.StartWithWindows = result.StartWithWindows;
            ViewModel.AppSettings.StartMinimized = result.StartMinimized;
            ViewModel.AppSettings.CloseToSystemTray = result.CloseToSystemTray;
            ViewModel.AppSettings.MinimizeToSystemTray = result.MinimizeToSystemTray;
            ViewModel.AppSettings.StartMainProgramWithStartAll = result.StartMainProgramWithStartAll;
            ViewModel.AppSettings.RunAsAdministrator = result.RunAsAdministrator;

            // Apply Start with Windows setting to registry
            StartupService.SetStartWithWindows(result.StartWithWindows);

            // Persist to disk
            ViewModel.SaveAppSettings();

            // If Run as Administrator was just enabled and we're not already running as admin, restart with elevation
            if (result.RunAsAdministrator && !wasRunAsAdmin && !AdminHelper.IsRunningAsAdministrator())
            {
                if (AdminHelper.RestartAsAdministrator())
                {
                    // Successfully started elevated process, close this one
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                }
                else
                {
                    // User cancelled or restart failed, revert the setting
                    ViewModel.AppSettings.RunAsAdministrator = false;
                    ViewModel.SaveAppSettings();
                }
            }
        }
    }

    private async Task<string?> ShowSaveProfileDialog(string? defaultFileName)
    {
        var storageProvider = StorageProvider;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Profile",
            SuggestedFileName = string.IsNullOrWhiteSpace(defaultFileName) ? "NewProfile.json" : $"{defaultFileName}.json",
            DefaultExtension = "json",
            SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(MainWindowViewModel.ProfilesFolder),
            FileTypeChoices =
            [
                new FilePickerFileType("Profile Files") { Patterns = ["*.json"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    private async Task<string?> ShowLoadProfileDialog()
    {
        var storageProvider = StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Profile",
            AllowMultiple = false,
            SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(MainWindowViewModel.ProfilesFolder),
            FileTypeFilter =
            [
                new FilePickerFileType("Profile Files") { Patterns = ["*.json"] }
            ]
        });

        return files != null && files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async Task<string?> ShowSelectMainProgramDialog(string? defaultPath)
    {
        var storageProvider = StorageProvider;

        var startLocation = await storageProvider.TryGetFolderFromPathAsync(defaultPath ?? @"C:\Program Files");

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Main Program/Game",
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable Files") { Patterns = ["*.exe"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] }
            ]
        });

        return files != null && files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}