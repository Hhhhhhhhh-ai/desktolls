using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskTolls.Services;

public sealed class AutoClickService : IDisposable
{
    private readonly Func<int> _clicksPerSecond;
    private CancellationTokenSource? _cancellation;
    private Task? _clickTask;

    public AutoClickService(Func<int> clicksPerSecond)
    {
        _clicksPerSecond = clicksPerSecond;
    }

    public event Action<bool>? StateChanged;

    public bool IsClicking => _cancellation is not null;

    public void Toggle()
    {
        if (IsClicking)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    public void Start()
    {
        if (IsClicking)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _clickTask = Task.Run(() => ClickLoop(token), token);
        StateChanged?.Invoke(true);
    }

    public void Stop()
    {
        var cancellation = Interlocked.Exchange(ref _cancellation, null);
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        var clickTask = Interlocked.Exchange(ref _clickTask, null);
        try
        {
            clickTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(inner => inner is TaskCanceledException))
        {
            // Cancellation is the expected way the click loop ends.
        }

        cancellation.Dispose();
        StateChanged?.Invoke(false);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void ClickLoop(CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextClick = stopwatch.ElapsedTicks;

        while (!token.IsCancellationRequested)
        {
            SendLeftClick();

            var clicksPerSecond = Math.Clamp(_clicksPerSecond(), 1, 100);
            var intervalTicks = Stopwatch.Frequency / clicksPerSecond;
            nextClick += intervalTicks;

            var remainingTicks = nextClick - stopwatch.ElapsedTicks;
            if (remainingTicks <= 0)
            {
                nextClick = stopwatch.ElapsedTicks;
                continue;
            }

            var remainingMilliseconds = remainingTicks * 1000 / Stopwatch.Frequency;
            if (remainingMilliseconds > 1 && token.WaitHandle.WaitOne((int)remainingMilliseconds - 1))
            {
                return;
            }

            while (!token.IsCancellationRequested && stopwatch.ElapsedTicks < nextClick)
            {
                Thread.SpinWait(40);
            }
        }
    }

    private static void SendLeftClick()
    {
        var inputs = new NativeMethods.Input[2];
        inputs[0].Type = NativeMethods.InputMouse;
        inputs[0].Mouse.Flags = NativeMethods.MouseeventfLeftdown;
        inputs[1].Type = NativeMethods.InputMouse;
        inputs[1].Mouse.Flags = NativeMethods.MouseeventfLeftup;

        NativeMethods.SendInput(
            unchecked((uint)inputs.Length),
            inputs,
            Marshal.SizeOf<NativeMethods.Input>());
    }
}
