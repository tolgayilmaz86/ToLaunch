using System;
using Avalonia;
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
        // Initialize logging first
        LogService.Initialize();

        // Check if the app should run as administrator on startup
        if (ShouldRestartAsAdmin())
        {
            if (AdminHelper.RestartAsAdministrator(args))
            {
                // Successfully started elevated process, exit this one
                return;
            }
            // If restart failed or was cancelled, continue running without elevation
        }

        // App reads these to tell a Windows-startup launch from a manual one.
        App.StartupArgs = args;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogService.LogError("Unhandled exception in application", ex);
            throw;
        }
    }

    private static bool ShouldRestartAsAdmin()
    {
        // Already running as admin, no need to restart
        if (AdminHelper.IsRunningAsAdministrator())
            return false;

        return SettingsService.Load().RunAsAdministrator;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
