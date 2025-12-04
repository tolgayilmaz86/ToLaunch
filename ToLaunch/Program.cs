using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using ToLaunch.Models;
using ToLaunch.Services;

namespace ToLaunch;
internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check if the app should run as administrator on startup
        if (ShouldRestartAsAdmin())
        {
            if (AdminHelper.RestartAsAdministrator())
            {
                // Successfully started elevated process, exit this one
                return;
            }
            // If restart failed or was cancelled, continue running without elevation
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool ShouldRestartAsAdmin()
    {
        // Already running as admin, no need to restart
        if (AdminHelper.IsRunningAsAdministrator())
            return false;

        // Check settings file for RunAsAdministrator preference
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

                return settings?.RunAsAdministrator == true;
            }
        }
        catch
        {
            // If we can't read settings, don't try to elevate
        }

        return false;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
