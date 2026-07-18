using Microsoft.Win32;

namespace DeskTolls.Services;

public sealed class ClassicContextMenuService
{
    private const string Clsid = "{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    private const string ClsidPath = @"Software\Classes\CLSID\" + Clsid;
    private const string InprocServerPath = ClsidPath + @"\InprocServer32";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InprocServerPath, false);
        return key is not null && string.Equals(key.GetValue(null) as string, string.Empty, StringComparison.Ordinal);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(InprocServerPath, true);
            key.SetValue(null, string.Empty, RegistryValueKind.String);
            return;
        }

        Registry.CurrentUser.DeleteSubKeyTree(ClsidPath, false);
    }
}
