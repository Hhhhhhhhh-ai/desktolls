using System.Runtime.InteropServices;

namespace DeskTolls.Services;

public sealed record TaskbarAutoHideState(bool AutoHideEnabled, bool AlwaysOnTop);

public sealed class TaskbarAutoHideService
{
    internal const uint AbmGetState = 0x00000004;
    internal const uint AbmSetState = 0x0000000A;
    internal const int AbsAutoHide = 0x00000001;
    internal const int AbsAlwaysOnTop = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    internal struct AppBarData
    {
        internal int Size;
        internal IntPtr Window;
        internal uint CallbackMessage;
        internal uint Edge;
        internal Rect Bounds;
        internal IntPtr Parameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    public TaskbarAutoHideState GetState()
    {
        var flags = GetStateFlags();
        return new TaskbarAutoHideState(
            (flags & AbsAutoHide) != 0,
            (flags & AbsAlwaysOnTop) != 0);
    }

    public TaskbarAutoHideState SetEnabled(bool enabled)
    {
        var currentFlags = GetStateFlags();
        var desiredFlags = ComposeStateFlags(currentFlags, enabled);
        var data = CreateAppBarData();
        data.Parameter = new IntPtr(desiredFlags);
        _ = SHAppBarMessage(AbmSetState, ref data);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var state = GetState();
            if (state.AutoHideEnabled == enabled)
            {
                return state;
            }

            Thread.Sleep(50);
        }

        throw new InvalidOperationException("Windows 未接受任务栏自动隐藏设置。");
    }

    internal static int ComposeStateFlags(int currentFlags, bool enabled)
    {
        return enabled
            ? currentFlags | AbsAutoHide
            : currentFlags & ~AbsAutoHide;
    }

    private static int GetStateFlags()
    {
        var data = CreateAppBarData();
        return unchecked((int)SHAppBarMessage(AbmGetState, ref data).ToUInt64());
    }

    private static AppBarData CreateAppBarData()
    {
        return new AppBarData
        {
            Size = Marshal.SizeOf<AppBarData>(),
        };
    }

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
}
