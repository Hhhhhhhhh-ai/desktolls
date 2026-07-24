using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeskTolls.Services;

public sealed class KeyboardShortcutService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly HashSet<int> _pressedModifiers = [];
    private IntPtr _hook;
    private bool _copyKeyDown;
    private bool _pasteKeyDown;

    public KeyboardShortcutService()
    {
        _callback = HookCallback;
    }

    public event EventHandler? CopyPressed;

    public event EventHandler? PastePressed;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_hook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装全局键盘钩子。");
        }

        SynchronizeModifierState();
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _copyKeyDown = false;
        _pasteKeyDown = false;
        _pressedModifiers.Clear();
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
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            var isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;

            if (isKeyDown || isKeyUp)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.KbdllHookStruct>(lParam);
                if ((hookData.Flags & NativeMethods.LlkhfInjected) == 0)
                {
                    ProcessKey(unchecked((int)hookData.VkCode), isKeyDown);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void ProcessKey(int virtualKey, bool isKeyDown)
    {
        if (IsModifierKey(virtualKey))
        {
            if (isKeyDown)
            {
                _pressedModifiers.Add(virtualKey);
            }
            else
            {
                _pressedModifiers.Remove(virtualKey);
            }

            return;
        }

        if (virtualKey == NativeMethods.VkC)
        {
            ProcessShortcutKey(ref _copyKeyDown, isKeyDown, CopyPressed);
        }
        else if (virtualKey == NativeMethods.VkV)
        {
            ProcessShortcutKey(ref _pasteKeyDown, isKeyDown, PastePressed);
        }
    }

    private void ProcessShortcutKey(
        ref bool keyDownState,
        bool isKeyDown,
        EventHandler? pressedHandler)
    {
        if (!isKeyDown)
        {
            keyDownState = false;
            return;
        }

        if (keyDownState)
        {
            return;
        }

        keyDownState = true;
        if (HasExactControlModifier())
        {
            pressedHandler?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool HasExactControlModifier()
    {
        var controlDown = _pressedModifiers.Contains(NativeMethods.VkControl)
            || _pressedModifiers.Contains(NativeMethods.VkLcontrol)
            || _pressedModifiers.Contains(NativeMethods.VkRcontrol);
        var otherModifierDown = _pressedModifiers.Any(key => key is
            NativeMethods.VkShift or NativeMethods.VkLshift or NativeMethods.VkRshift
            or NativeMethods.VkMenu or NativeMethods.VkLmenu or NativeMethods.VkRmenu
            or NativeMethods.VkLwin or NativeMethods.VkRwin);
        return controlDown && !otherModifierDown;
    }

    private void SynchronizeModifierState()
    {
        _pressedModifiers.Clear();
        foreach (var virtualKey in ModifierKeys)
        {
            if ((NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                _pressedModifiers.Add(virtualKey);
            }
        }
    }

    private static bool IsModifierKey(int virtualKey)
    {
        return virtualKey is
            NativeMethods.VkControl or NativeMethods.VkLcontrol or NativeMethods.VkRcontrol
            or NativeMethods.VkShift or NativeMethods.VkLshift or NativeMethods.VkRshift
            or NativeMethods.VkMenu or NativeMethods.VkLmenu or NativeMethods.VkRmenu
            or NativeMethods.VkLwin or NativeMethods.VkRwin;
    }

    private static IReadOnlyList<int> ModifierKeys { get; } =
    [
        NativeMethods.VkLcontrol,
        NativeMethods.VkRcontrol,
        NativeMethods.VkLshift,
        NativeMethods.VkRshift,
        NativeMethods.VkLmenu,
        NativeMethods.VkRmenu,
        NativeMethods.VkLwin,
        NativeMethods.VkRwin,
    ];
}
