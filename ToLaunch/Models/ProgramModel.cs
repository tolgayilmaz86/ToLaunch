namespace ToLaunch.Models;
public class ProgramModel
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // Start/Stop Options
    public bool StartHidden { get; set; }
    public bool StopOnExit { get; set; } = true;
    public bool StartWithProgram { get; set; }
    public string? StartWithProgramName { get; set; }
    public bool StopWithProgram { get; set; }
    public string? StopWithProgramName { get; set; }

    // Delay Options
    public int DelayStartSeconds { get; set; }
    public int DelayStopSeconds { get; set; }

    // Priority Option
    public ProcessPriority Priority { get; set; } = ProcessPriority.Default;

    // CPU Affinity - stores the bitmask of selected cores (0 = use all cores/default)
    public long CpuAffinity { get; set; } = 0;
}