using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeskTolls.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4454;
    private readonly IntPtr _window;
    private int? _registeredVirtualKey;

    public HotkeyService(IntPtr window)
    {
        _window = window;
    }

    public int? RegisteredVirtualKey => _registeredVirtualKey;

    public void Register(int virtualKey)
    {
        if (_registeredVirtualKey == virtualKey)
        {
            return;
        }

        var previousKey = _registeredVirtualKey;
        Unregister();

        if (NativeMethods.RegisterHotKey(
                _window,
                HotkeyId,
                NativeMethods.ModNoRepeat,
                unchecked((uint)virtualKey)))
        {
            _registeredVirtualKey = virtualKey;
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (previousKey.HasValue && NativeMethods.RegisterHotKey(
                _window,
                HotkeyId,
                NativeMethods.ModNoRepeat,
                unchecked((uint)previousKey.Value)))
        {
            _registeredVirtualKey = previousKey;
        }

        throw new Win32Exception(error, "该按键已被其他程序占用，无法注册为全局热键。");
    }

    public void Unregister()
    {
        if (!_registeredVirtualKey.HasValue)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_window, HotkeyId);
        _registeredVirtualKey = null;
    }

    public static bool IsHotkeyMessage(int message, IntPtr wParam)
    {
        return message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }
}
