using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ToLaunch.Services;
using ToLaunch.ViewModels;
using ToLaunch.Views;

namespace ToLaunch;
public partial class App : Application
{
    /// <summary>
    /// Command line argument that the Windows "Run" registry entry appends, marking a launch that came
    /// from Windows startup rather than from the user.
    /// </summary>
    public const string StartupArgument = "--startup";

    /// <summary>Command line arguments, captured by <see cref="Program.Main"/>.</summary>
    public static string[] StartupArgs { get; set; } = [];

    private MainWindowViewModel? _viewModel;
    private MainWindow? _mainWindow;
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
            _mainWindow = new MainWindow
            {
                DataContext = _viewModel,
            };

            // Always own the window, even when starting to tray: the tray menu needs something to show,
            // and creating it later would leave platform resources (bitmaps, popups) uninitialised.
            desktop.MainWindow = _mainWindow;

            // Avalonia shows MainWindow itself once this method returns, so starting to tray means
            // hiding it the moment it opens rather than declining to create it.
            if (ShouldStartMinimized())
            {
                _mainWindow.ShowInTaskbar = false;
                _mainWindow.Opened += HideOnFirstOpen;
            }

            desktop.Exit += OnExit;
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

            // Profiles is a get-only collection that never raises PropertyChanged, so the menu has to
            // track the collection itself.
            _viewModel.Profiles.CollectionChanged += Profiles_CollectionChanged;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// True when this launch should go straight to the tray: the setting is on <em>and</em> Windows
    /// started us. Launching ToLaunch by hand always shows the window.
    /// </summary>
    private static bool ShouldStartMinimized()
    {
        bool launchedByWindows = Array.Exists(
            StartupArgs,
            arg => string.Equals(arg, StartupArgument, StringComparison.OrdinalIgnoreCase));

        return launchedByWindows && SettingsService.Load().StartMinimized;
    }

    private void HideOnFirstOpen(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Opened -= HideOnFirstOpen;
            window.Hide();
        }
    }

    public void TrayIcon_Show_Click(object? sender, EventArgs e)
    {
        if (_mainWindow == null)
            return;

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void TrayIcon_Exit_Click(object? sender, EventArgs e)
    {
        // The window cancels Closing when "Close to System Tray" is on, which would also cancel the
        // shutdown. Tell it this close is the real thing.
        _mainWindow?.PrepareForShutdown();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void TrayIcon_StartAll_Click(object? sender, EventArgs e)
    {
        _viewModel?.StartAllPrograms();
    }

    public void TrayIcon_StopAll_Click(object? sender, EventArgs e)
    {
        _viewModel?.StopAllPrograms();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _viewModel?.Dispose();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedProfile))
        {
            RebuildTrayMenu();
        }
    }

    private void Profiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Menu = BuildTrayMenu();
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
                var profileName = profile;
                var profileItem = new NativeMenuItem(profileName)
                {
                    ToggleType = NativeMenuItemToggleType.Radio,
                    IsChecked = profileName == _viewModel.SelectedProfile
                };
                profileItem.Click += (s, e) =>
                {
                    if (_viewModel != null)
                        _viewModel.SelectedProfile = profileName;
                };
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
