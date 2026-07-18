using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeskTolls.Services;

public sealed class MouseHookService : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private IntPtr _hook;
    private bool _suppressMiddleUntilUp;

    public MouseHookService()
    {
        _callback = HookCallback;
    }

    public event EventHandler? DesktopMiddlePressed;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_hook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装全局鼠标钩子。");
        }
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _suppressMiddleUntilUp = false;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WmMButtonDown)
        {
            var hookData = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);
            if (DesktopIconService.IsDesktopPoint(hookData.Point))
            {
                _suppressMiddleUntilUp = true;
                DesktopMiddlePressed?.Invoke(this, EventArgs.Empty);
                return new IntPtr(1);
            }
        }

        if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WmMButtonUp && _suppressMiddleUntilUp)
        {
            _suppressMiddleUntilUp = false;
            return new IntPtr(1);
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
