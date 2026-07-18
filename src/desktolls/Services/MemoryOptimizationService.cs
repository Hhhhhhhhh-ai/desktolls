using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskTolls.Services;

public sealed record MemoryOptimizationResult(
    DateTime CompletedAt,
    long WorkingSetBefore,
    long WorkingSetAfter,
    long ManagedBytesAfter,
    bool Success,
    int Win32Error)
{
    public long WorkingSetReduction => Math.Max(0, WorkingSetBefore - WorkingSetAfter);
}

public sealed class MemoryOptimizationService : IDisposable
{
    private readonly Func<int> _intervalSeconds;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _optimizationGate = new(1, 1);
    private CancellationTokenSource? _cancellation;
    private Task? _loopTask;
    private bool _disposed;

    public MemoryOptimizationService(Func<int> intervalSeconds)
    {
        _intervalSeconds = intervalSeconds;
    }

    public event Action<MemoryOptimizationResult>? Optimized;

    public bool IsRunning => Volatile.Read(ref _cancellation) is not null;

    public void Start()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cancellation is not null)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _loopTask = Task.Run(() => OptimizationLoopAsync(_cancellation.Token));
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation;
        Task? loopTask;

        lock (_stateLock)
        {
            cancellation = _cancellation;
            loopTask = _loopTask;
            _cancellation = null;
            _loopTask = null;
        }

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(inner => inner is TaskCanceledException))
        {
            // Cancellation is the expected way the periodic loop ends.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    public async Task<MemoryOptimizationResult?> OptimizeNowAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await _optimizationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var result = await Task.Run(OptimizeCurrentProcess, cancellationToken).ConfigureAwait(false);
            Optimized?.Invoke(result);
            return result;
        }
        finally
        {
            _optimizationGate.Release();
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
        _optimizationGate.Wait();
        _optimizationGate.Release();
        _optimizationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task OptimizationLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var interval = TimeSpan.FromSeconds(Math.Clamp(_intervalSeconds(), 10, 600));
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                await OptimizeNowAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown or interval restart.
        }
    }

    private static MemoryOptimizationResult OptimizeCurrentProcess()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var workingSetBefore = process.WorkingSet64;

        var success = NativeMethods.EmptyWorkingSet(process.Handle);
        var error = success ? 0 : Marshal.GetLastWin32Error();

        process.Refresh();
        return new MemoryOptimizationResult(
            DateTime.Now,
            workingSetBefore,
            process.WorkingSet64,
            GC.GetTotalMemory(false),
            success,
            error);
    }
}
