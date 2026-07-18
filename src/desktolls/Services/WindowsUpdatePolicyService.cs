using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace DeskTolls.Services;

public sealed record WindowsUpdatePolicyState(
    bool AutomaticUpdatesDisabled,
    bool ManagedByDesktolls);

public static class WindowsUpdatePolicyService
{
    internal const string CommandPrefix = "--windows-update-policy=";

    private const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string BackupPath = @"SOFTWARE\desktolls\Backups\WindowsUpdate";
    private const string NoAutoUpdate = "NoAutoUpdate";
    private const string AuOptions = "AUOptions";
    private const string BackupCaptured = "BackupCaptured";
    private const string AppliedByDesktolls = "AppliedByDesktolls";

    public static WindowsUpdatePolicyState GetState()
    {
        using var policyKey = Registry.LocalMachine.OpenSubKey(PolicyPath, false);
        using var backupKey = Registry.LocalMachine.OpenSubKey(BackupPath, false);

        return new WindowsUpdatePolicyState(
            ReadDword(policyKey, NoAutoUpdate) == 1,
            ReadDword(backupKey, AppliedByDesktolls) == 1);
    }

    public static async Task<int> SetAutomaticUpdatesDisabledAsync(bool disabled)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定 desktolls 的程序路径。");
        var command = disabled ? "disable" : "restore";

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = CommandPrefix + command,
            UseShellExecute = true,
            Verb = "runas",
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动管理员策略进程。");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    public static bool TryGetElevatedCommand(IEnumerable<string> arguments, out string command)
    {
        var argument = arguments.FirstOrDefault(value =>
            value.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase));
        command = argument?[CommandPrefix.Length..].ToLowerInvariant() ?? string.Empty;
        return command is "disable" or "restore";
    }

    public static int ExecuteElevatedCommand(string command)
    {
        if (!IsAdministrator())
        {
            return 5;
        }

        try
        {
            if (string.Equals(command, "disable", StringComparison.OrdinalIgnoreCase))
            {
                DisableAutomaticUpdates();
            }
            else if (string.Equals(command, "restore", StringComparison.OrdinalIgnoreCase))
            {
                RestoreAutomaticUpdates();
            }
            else
            {
                return 64;
            }

            return 0;
        }
        catch (Exception exception)
        {
            WriteErrorLog(exception);
            return 10;
        }
    }

    private static void DisableAutomaticUpdates()
    {
        using var policyKey = Registry.LocalMachine.CreateSubKey(PolicyPath, true);
        using var backupKey = Registry.LocalMachine.CreateSubKey(BackupPath, true);

        if (ReadDword(backupKey, BackupCaptured) != 1)
        {
            BackupDword(policyKey, backupKey, NoAutoUpdate);
            BackupDword(policyKey, backupKey, AuOptions);
            backupKey.SetValue(BackupCaptured, 1, RegistryValueKind.DWord);
            backupKey.SetValue(
                "CapturedAtUtc",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                RegistryValueKind.String);
        }

        policyKey.SetValue(NoAutoUpdate, 1, RegistryValueKind.DWord);
        policyKey.SetValue(AuOptions, 1, RegistryValueKind.DWord);
        backupKey.SetValue(AppliedByDesktolls, 1, RegistryValueKind.DWord);
    }

    private static void RestoreAutomaticUpdates()
    {
        using var policyKey = Registry.LocalMachine.CreateSubKey(PolicyPath, true);
        using var backupKey = Registry.LocalMachine.OpenSubKey(BackupPath, false);

        if (backupKey is not null && ReadDword(backupKey, BackupCaptured) == 1)
        {
            RestoreDword(policyKey, backupKey, NoAutoUpdate);
            RestoreDword(policyKey, backupKey, AuOptions);
        }
        else
        {
            policyKey.DeleteValue(NoAutoUpdate, false);
            policyKey.DeleteValue(AuOptions, false);
        }

        Registry.LocalMachine.DeleteSubKeyTree(BackupPath, false);
    }

    private static void BackupDword(RegistryKey policyKey, RegistryKey backupKey, string valueName)
    {
        var existingValue = policyKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        backupKey.SetValue($"Had_{valueName}", existingValue is null ? 0 : 1, RegistryValueKind.DWord);

        if (existingValue is not null)
        {
            backupKey.SetValue(
                $"Value_{valueName}",
                Convert.ToInt32(existingValue, CultureInfo.InvariantCulture),
                RegistryValueKind.DWord);
        }
    }

    private static void RestoreDword(RegistryKey policyKey, RegistryKey backupKey, string valueName)
    {
        if (ReadDword(backupKey, $"Had_{valueName}") == 1)
        {
            policyKey.SetValue(
                valueName,
                ReadDword(backupKey, $"Value_{valueName}") ?? 0,
                RegistryValueKind.DWord);
        }
        else
        {
            policyKey.DeleteValue(valueName, false);
        }
    }

    private static int? ReadDword(RegistryKey? key, string valueName)
    {
        var value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is null
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void WriteErrorLog(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.SettingsDirectory);
            File.AppendAllText(
                Path.Combine(SettingsStore.SettingsDirectory, "windows-update-policy-error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch
        {
            // Policy errors are returned through the helper exit code as well.
        }
    }
}
