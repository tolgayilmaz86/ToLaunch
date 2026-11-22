using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ToLaunch.Services;

/// <summary>
/// Periodically captures a <see cref="ProcessSnapshot"/> and hands it to a callback on the UI thread.
/// </summary>
/// <remarks>
/// The expensive process enumeration runs on the thread pool; the watched paths are read on the UI
/// thread (they come from observable collections) and the callback is invoked there too, so consumers
/// never have to marshal. Ticks cannot overlap: if one runs long, the next is skipped rather than
/// queued, and the timer is re-armed only once a tick has finished.
/// </remarks>
public sealed class ProcessMonitorService : IDisposable
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly Func<IReadOnlyList<string?>> _watchedPathsProvider;
    private readonly Action<ProcessSnapshot> _onSnapshot;
    private readonly TimeSpan _interval;

    private Timer? _timer;
    private int _tickInProgress;
    private volatile bool _disposed;

    /// <param name="watchedPathsProvider">
    /// Returns the executable paths to watch. Invoked on the UI thread before each capture.
    /// </param>
    /// <param name="onSnapshot">
    /// Receives each snapshot on the UI thread. The snapshot is disposed once this returns, so it must
    /// not be retained.
    /// </param>
    public ProcessMonitorService(
        Func<IReadOnlyList<string?>> watchedPathsProvider,
        Action<ProcessSnapshot> onSnapshot,
        TimeSpan? interval = null)
    {
        _watchedPathsProvider = watchedPathsProvider ?? throw new ArgumentNullException(nameof(watchedPathsProvider));
        _onSnapshot = onSnapshot ?? throw new ArgumentNullException(nameof(onSnapshot));
        _interval = interval ?? DefaultInterval;
    }

    /// <summary>Begins monitoring. The first tick runs immediately.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Infinite period: the timer is re-armed after each tick so ticks can never overlap.
        _timer ??= new Timer(OnTimerTick, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Requests an immediate tick, so state is refreshed without waiting out the interval.
    /// Used after the set of watched programs changes.
    /// </summary>
    public void PollNow()
    {
        if (_disposed || _timer == null)
            return;

        try
        {
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Raced with Dispose; nothing to refresh.
        }
    }

    private async void OnTimerTick(object? state)
    {
        // Drop this tick if the previous one is still running.
        if (Interlocked.CompareExchange(ref _tickInProgress, 1, 0) != 0)
            return;

        try
        {
            var watchedPaths = await Dispatcher.UIThread.InvokeAsync(_watchedPathsProvider);
            if (_disposed)
                return;

            using var snapshot = await Task.Run(() => ProcessSnapshot.Capture(watchedPaths));
            if (_disposed)
                return;

            await Dispatcher.UIThread.InvokeAsync(() => _onSnapshot(snapshot));
        }
        catch (Exception ex)
        {
            LogService.LogError("Process monitor tick failed", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _tickInProgress, 0);
            Rearm();
        }
    }

    private void Rearm()
    {
        if (_disposed || _timer == null)
            return;

        try
        {
            _timer.Change(_interval, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Disposed while the tick was running; stop quietly.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
