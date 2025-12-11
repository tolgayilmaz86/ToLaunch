namespace ToLaunch.Models;

using System.Collections.Generic;

public class AppSettings
{
    public string? LastSelectedProfile { get; set; }
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = false;
    public bool CloseToSystemTray { get; set; }
    public bool MinimizeToSystemTray { get; set; }
    public bool StartMainProgramWithStartAll { get; set; } = false;
    public bool RunAsAdministrator { get; set; }
}
