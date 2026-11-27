using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ToLaunch.Services;

/// <summary>
/// A point-in-time index of the running processes the application cares about.
/// </summary>
/// <remarks>
/// <para>
/// Enumerating the process table is the single most expensive thing this application does, so a
/// snapshot is captured once and then queried many times instead of calling
/// <see cref="Process.GetProcessesByName(string)"/> per card. Only processes whose executable name
/// matches a watched path are retained; everything else is disposed immediately. When nothing is
/// being watched the process table is never enumerated at all.
/// </para>
/// <para>
/// Lookups match on the full executable path, also accepting a process running from inside the
/// configured executable's own directory tree (how launcher stubs such as Squirrel behave), and
/// falling back to the executable name when a candidate's path cannot be read at all. Matching on the
/// name alone would let a card act on an unrelated program that merely happens to share its file name.
/// </para>
/// </remarks>
public sealed class ProcessSnapshot : IDisposable
{
    private static readonly IReadOnlyList<Process> NoMatches = [];

    /// <summary>Executable name (without extension) to the live processes carrying that name.</summary>
    private readonly Dictionary<string, List<Process>> _byName;

    /// <summary>Resolved image paths, cached per process id for this snapshot's lifetime.</summary>
    private readonly Dictionary<int, string?> _imagePaths = [];

    private bool _disposed;

    private ProcessSnapshot(Dictionary<string, List<Process>> byName) => _byName = byName;

    /// <summary>
    /// Captures the processes matching any of <paramref name="watchedPaths"/>. Entries that are null,
    /// blank or unparseable are ignored.
    /// </summary>
    public static ProcessSnapshot Capture(IEnumerable<string?> watchedPaths)
    {
        ArgumentNullException.ThrowIfNull(watchedPaths);

        var watchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in watchedPaths)
        {
            var name = TryGetFileNameWithoutExtension(path);
            if (name != null)
                watchedNames.Add(name);
        }

        var byName = new Dictionary<string, List<Process>>(StringComparer.OrdinalIgnoreCase);

        // Nothing configured: skip the process enumeration entirely.
        if (watchedNames.Count == 0)
            return new ProcessSnapshot(byName);

        foreach (var process in Process.GetProcesses())
        {
            string processName;
            try
            {
                processName = process.ProcessName;
            }
            catch (Exception)
            {
                // The process exited between enumeration and inspection.
                process.Dispose();
                continue;
            }

            if (!watchedNames.Contains(processName))
            {
                process.Dispose();
                continue;
            }

            if (!byName.TryGetValue(processName, out var matches))
            {
                matches = [];
                byName[processName] = matches;
            }

            matches.Add(process);
        }

        return new ProcessSnapshot(byName);
    }

    /// <summary>
    /// Returns true when at least one running process corresponds to <paramref name="programPath"/>.
    /// </summary>
    public bool IsRunning(string? programPath) => Find(programPath).Count > 0;

    /// <summary>
    /// Returns the running processes corresponding to <paramref name="programPath"/>.
    /// The returned <see cref="Process"/> instances are owned by this snapshot and must not be disposed
    /// by the caller.
    /// </summary>
    public IReadOnlyList<Process> Find(string? programPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var name = TryGetFileNameWithoutExtension(programPath);
        if (name == null || !_byName.TryGetValue(name, out var candidates))
            return NoMatches;

        var expectedPath = TryGetFullPath(programPath!);

        // The configured path cannot be normalised, so the name match is the best available answer.
        if (expectedPath == null)
            return candidates;

        var expectedDirectory = GetContainingDirectoryForMatching(expectedPath);

        List<Process>? matches = null;
        foreach (var candidate in candidates)
        {
            if (IsMatch(GetImagePath(candidate), expectedPath, expectedDirectory))
                (matches ??= []).Add(candidate);
        }

        return matches ?? NoMatches;
    }

    /// <summary>
    /// Decides whether a name-matched process really is the configured program.
    /// </summary>
    /// <remarks>
    /// Three cases count as a match:
    /// <list type="number">
    /// <item>the paths are equal, the straightforward case;</item>
    /// <item>the running image sits inside the configured executable's own directory tree. Launcher
    /// stubs do this: Squirrel installs <c>%LOCALAPPDATA%\App\App.exe</c> as a shim that runs
    /// <c>%LOCALAPPDATA%\Apppp-1.2.3\App.exe</c>, so the process never reports the path the user
    /// picked. The shared directory keeps this from matching an unrelated program;</item>
    /// <item>the running image's path could not be read at all (an elevated process, say), where the
    /// name match is the only evidence available.</item>
    /// </list>
    /// </remarks>
    private static bool IsMatch(string? actualPath, string expectedPath, string? expectedDirectory)
    {
        // Case 3: nothing to compare against, so trust the name.
        if (actualPath == null)
            return true;

        // Case 1.
        if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // Case 2.
        return expectedDirectory != null
            && actualPath.StartsWith(expectedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The configured executable's directory, with a trailing separator, for the subtree test.
    /// Returns null when that directory is a volume root, since "anywhere on this drive" is too broad
    /// to tell two programs apart.
    /// </summary>
    private static string? GetContainingDirectoryForMatching(string expectedPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(expectedPath);
            if (string.IsNullOrEmpty(directory))
                return null;

            // A path like "C:\" or "\server\share" is its own root; refuse to match on it.
            if (string.Equals(Path.GetPathRoot(directory), directory, StringComparison.OrdinalIgnoreCase))
                return null;

            return directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private string? GetImagePath(Process process)
    {
        int processId;
        try
        {
            processId = process.Id;
        }
        catch (Exception)
        {
            return null;
        }

        if (_imagePaths.TryGetValue(processId, out var cached))
            return cached;

        var resolved = NativeMethods.TryGetProcessImagePath(processId);
        if (resolved != null)
            resolved = TryGetFullPath(resolved);

        _imagePaths[processId] = resolved;
        return resolved;
    }

    private static string? TryGetFileNameWithoutExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (ArgumentException)
        {
            // Invalid path characters.
            return null;
        }
    }

    private static string? TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var matches in _byName.Values)
        {
            foreach (var process in matches)
            {
                process.Dispose();
            }
        }

        _byName.Clear();
        _imagePaths.Clear();
    }
}
