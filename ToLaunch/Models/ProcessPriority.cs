namespace ToLaunch.Models;

/// <summary>
/// Specifies the priority level for launched processes.
/// </summary>
public enum ProcessPriority
{
    /// <summary>
    /// Let Windows decide the priority (Normal priority).
    /// </summary>
    Default = 0,

    /// <summary>
    /// Low priority - process will yield to other processes.
    /// </summary>
    Low = 1,

    /// <summary>
    /// High priority - process will get more CPU time.
    /// </summary>
    High = 2
}
