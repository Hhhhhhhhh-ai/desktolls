using Microsoft.Win32;

namespace DeskTolls.Services;

public sealed class DesktopIconService
{
    private const string ExplorerAdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string HideIconsValue = "HideIcons";
    private static readonly IntPtr ToggleDesktopIconsCommand = new(0x7402);

    public bool? AreIconsVisible()
    {
        var listView = FindDesktopListView();
        return listView == IntPtr.Zero ? null : NativeMethods.IsWindowVisible(listView);
    }

    public bool GetDesiredVisibilityFromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedPath, false);
        return Convert.ToInt32(key?.GetValue(HideIconsValue, 0) ?? 0) == 0;
    }

    public async Task<bool> SetIconsVisibleAsync(bool visible, int attempts = 8)
    {
        WriteDesiredVisibility(visible);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (SetShellVisibility(visible))
            {
                return true;
            }

            await Task.Delay(350);
        }

        return false;
    }

    public async Task<bool> ToggleAsync()
    {
        var current = AreIconsVisible() ?? GetDesiredVisibilityFromRegistry();
        return await SetIconsVisibleAsync(!current);
    }

    internal static bool IsDesktopPoint(NativeMethods.Point point)
    {
        var window = NativeMethods.WindowFromPoint(point);
        if (window == IntPtr.Zero)
        {
            return false;
        }

        for (var current = window; current != IntPtr.Zero; current = NativeMethods.GetParent(current))
        {
            var className = NativeMethods.GetWindowClass(current);
            if (className is "SysListView32" or "SHELLDLL_DefView" or "Progman" or "WorkerW")
            {
                return true;
            }
        }

        var rootClass = NativeMethods.GetWindowClass(NativeMethods.GetAncestor(window, NativeMethods.GaRoot));
        return rootClass is "Progman" or "WorkerW";
    }

    private static bool SetShellVisibility(bool visible)
    {
        var listView = FindDesktopListView();
        if (listView == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.IsWindowVisible(listView) == visible)
        {
            return true;
        }

        var progman = NativeMethods.FindWindow("Progman", "Program Manager");
        if (progman != IntPtr.Zero)
        {
            NativeMethods.SendMessage(progman, NativeMethods.WmCommand, ToggleDesktopIconsCommand, IntPtr.Zero);
        }

        if (NativeMethods.IsWindowVisible(listView) == visible)
        {
            return true;
        }

        var shellHost = NativeMethods.GetParent(NativeMethods.GetParent(listView));
        if (shellHost != IntPtr.Zero && shellHost != progman)
        {
            NativeMethods.SendMessage(shellHost, NativeMethods.WmCommand, ToggleDesktopIconsCommand, IntPtr.Zero);
        }

        if (NativeMethods.IsWindowVisible(listView) == visible)
        {
            return true;
        }

        NativeMethods.ShowWindow(listView, visible ? NativeMethods.SwShow : NativeMethods.SwHide);
        return NativeMethods.IsWindowVisible(listView) == visible;
    }

    private static void WriteDesiredVisibility(bool visible)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ExplorerAdvancedPath, true);
        key.SetValue(HideIconsValue, visible ? 0 : 1, RegistryValueKind.DWord);
    }

    private static IntPtr FindDesktopListView()
    {
        var shellView = FindShellView();
        if (shellView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var listView = NativeMethods.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
        return listView != IntPtr.Zero
            ? listView
            : NativeMethods.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
    }

    private static IntPtr FindShellView()
    {
        var progman = NativeMethods.FindWindow("Progman", "Program Manager");
        var shellView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellView != IntPtr.Zero)
        {
            return shellView;
        }

        NativeMethods.EnumWindows((window, _) =>
        {
            shellView = NativeMethods.FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
            return shellView == IntPtr.Zero;
        }, IntPtr.Zero);

        return shellView;
    }
}
