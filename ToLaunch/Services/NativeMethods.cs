using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ToLaunch.Services;

/// <summary>
/// Win32 entry points shared by the process-related services.
/// </summary>
internal static class NativeMethods
{
    // Process access rights
    internal const uint PROCESS_SET_INFORMATION = 0x0200;
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // Priority class constants
    internal const uint IDLE_PRIORITY_CLASS = 0x00000040;
    internal const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    internal const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    internal const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    internal const uint HIGH_PRIORITY_CLASS = 0x00000080;
    internal const uint REALTIME_PRIORITY_CLASS = 0x00000100;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetPriorityClass(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    /// <summary>
    /// Returns the full image path of a process, or <c>null</c> when it cannot be determined
    /// (the process has exited, or the caller lacks the rights to query it).
    /// </summary>
    /// <remarks>
    /// Uses <c>QueryFullProcessImageName</c> rather than <see cref="System.Diagnostics.Process.MainModule"/>:
    /// it needs only PROCESS_QUERY_LIMITED_INFORMATION, so it succeeds against elevated and
    /// cross-bitness processes far more often, and it does not enumerate the module list.
    /// </remarks>
    internal static string? TryGetProcessImagePath(int processId)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            // 32767 is the documented maximum extended path length.
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;

            if (QueryFullProcessImageNameW(handle, 0, buffer, ref size))
                return buffer.ToString(0, (int)size);

            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
