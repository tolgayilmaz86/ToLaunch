using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ToLaunch.Models;
using ToLaunch.ViewModels;
using Avalonia.Platform;
using System.ComponentModel;
using ToLaunch.Views;
using ToLaunch.Services;

namespace ToLaunch;
public partial class App : Application
{
    private MainWindowViewModel? _viewModel;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _viewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = _viewModel,
            };

            if (!ShouldStartMinimized())
            {
                desktop.MainWindow = mainWindow;
            }
            else
            {
                // If starting minimized, ensure Avalonia platform is initialized
                // by showing a tiny invisible window briefly. This avoids creating
                // platform-dependent resources (bitmaps/popups) while the platform
                // is uninitialized when changing profile from the tray.
                try
                {
                    var initWindow = new Window
                    {
                        Width = 0,
                        Height = 0,
                        ShowInTaskbar = false,
                        Opacity = 0
                    };
                    initWindow.Opened += (s, e) => initWindow.Hide();
                    // Show will initialize platform; hide immediately in Opened.
                    initWindow.Show();
                }
                catch
                {
                    // Best-effort, ignore failures here
                }
            }
        }

        // Setup tray icon
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ToLaunch/Assets/ToLaunch.ico"))),
            ToolTipText = "ToLaunch"
        };
        _trayIcon.Clicked += TrayIcon_Show_Click;
        _trayIcon.Menu = BuildTrayMenu();
        TrayIcon.SetIcons(this, [_trayIcon]);

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Checks the settings to see if the app should start minimized.
    /// </summary>
    private static bool ShouldStartMinimized()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ToLaunch", "settings.json");

            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return settings?.StartMinimized ?? false;
            }
        }
        catch
        {
            // If we can't read settings, default to not starting minimized
        }

        return false;
    }

    public void TrayIcon_Show_Click(object? sender, System.EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null)
            {
                var mainWindow = new MainWindow
                {
                    DataContext = _viewModel,
                };
                desktop.MainWindow = mainWindow;
            }
            var window = desktop.MainWindow;
            if (window != null)
            {
                window.ShowInTaskbar = true;
                window.Show();
                window.Activate();
            }
        }
    }

    public void TrayIcon_Exit_Click(object? sender, System.EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void TrayIcon_StartAll_Click(object? sender, System.EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = desktop.MainWindow?.DataContext as MainWindowViewModel ?? _viewModel;
            if (viewModel != null)
            {
                // Start all enabled programs that are not running
                foreach (var card in viewModel.LaunchCards)
                {
                    if (card.IsEnabled && !card.IsRunning)
                    {
                        card.StartCommand.Execute(null);
                    }
                }
            }
        }
    }

    public void TrayIcon_StopAll_Click(object? sender, System.EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = desktop.MainWindow?.DataContext as MainWindowViewModel ?? _viewModel;
            if (viewModel != null)
            {
                // Stop all enabled programs that are actually running (check by process name)
                foreach (var card in viewModel.LaunchCards)
                {
                    if (card.IsEnabled && !string.IsNullOrWhiteSpace(card.Path))
                    {
                        // Check if the process is actually running, not just the IsRunning flag
                        if (card.IsRunning || Services.ProgramLaunchService.IsProgramAlreadyRunning(card.Path))
                        {
                            card.StartCommand.Execute(null);
                        }
                    }
                }
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedProfile) || e.PropertyName == nameof(MainWindowViewModel.Profiles))
        {
            if (_trayIcon != null)
            {
                _trayIcon.Menu = BuildTrayMenu();
            }
        }
    }

    private NativeMenu BuildTrayMenu()
    {
        var menu = new NativeMenu();

        // Show
        var showItem = new NativeMenuItem("Show");
        showItem.Click += TrayIcon_Show_Click;
        menu.Add(showItem);

        menu.Add(new NativeMenuItemSeparator());

        // Start All
        var startAllItem = new NativeMenuItem("Start All");
        startAllItem.Click += TrayIcon_StartAll_Click;
        menu.Add(startAllItem);

        // Stop All
        var stopAllItem = new NativeMenuItem("Stop All");
        stopAllItem.Click += TrayIcon_StopAll_Click;
        menu.Add(stopAllItem);

        menu.Add(new NativeMenuItemSeparator());

        // Profiles submenu
        var profilesItem = new NativeMenuItem("Profiles");
        var profilesMenu = new NativeMenu();
        if (_viewModel != null)
        {
            foreach (var profile in _viewModel.Profiles)
            {
                var profileItem = new NativeMenuItem(profile)
                {
                    ToggleType = NativeMenuItemToggleType.Radio,
                    IsChecked = profile == _viewModel.SelectedProfile
                };
                profileItem.Click += (s, e) => { if (_viewModel != null) _viewModel.SelectedProfile = profile; };
                profilesMenu.Add(profileItem);
            }
        }
        profilesItem.Menu = profilesMenu;
        menu.Add(profilesItem);

        menu.Add(new NativeMenuItemSeparator());

        // Exit
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += TrayIcon_Exit_Click;
        menu.Add(exitItem);

        return menu;
    }
}