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
using ToLaunch.Views;

namespace ToLaunch;
public partial class App : Application
{
    private MainWindowViewModel? _viewModel;

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
}