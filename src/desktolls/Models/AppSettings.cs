using System.IO;

namespace DeskTolls.Models;

public sealed class AppSettings
{
    public bool DesktopToggleEnabled { get; set; } = true;

    public bool ClassicContextMenuEnabled { get; set; } = true;

    public bool AutoClickEnabled { get; set; } = true;

    public int AutoClickHotkey { get; set; } = 0x77; // F8

    public int ClicksPerSecond { get; set; } = 10;

    public bool CopySoundEnabled { get; set; } = true;

    public bool PasteSoundEnabled { get; set; } = true;

    public bool TaskbarAutoHideEnabled { get; set; }

    public bool RestoreSystemSettingsOnExit { get; set; } = true;

    public bool MemoryOptimizationEnabled { get; set; } = true;

    public int MemoryOptimizationIntervalSeconds { get; set; } = 10;

    public bool StartWithWindows { get; set; } = true;

    public bool HideIconsOnStartup { get; set; } = true;

    public string DownloadFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads");

    public int DownloadThreadCount { get; set; } = 4;

    public bool AutoDetectDownloadFileName { get; set; } = true;

    public void Normalize()
    {
        ClicksPerSecond = Math.Clamp(ClicksPerSecond, 1, 100);

        if (!MemoryOptimizationIntervalOption.All.Any(option =>
                option.Seconds == MemoryOptimizationIntervalSeconds))
        {
            MemoryOptimizationIntervalSeconds = 10;
        }

        if (!HotkeyOption.All.Any(option => option.VirtualKey == AutoClickHotkey))
        {
            AutoClickHotkey = 0x77;
        }

        if (!DownloadThreadOption.All.Any(option => option.Count == DownloadThreadCount))
        {
            DownloadThreadCount = 4;
        }

        if (string.IsNullOrWhiteSpace(DownloadFolder))
        {
            DownloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
        }
    }
}

public sealed record DownloadThreadOption(string Name, int Count)
{
    public static IReadOnlyList<DownloadThreadOption> All { get; } =
    [
        new("单线程", 1),
        new("2 线程", 2),
        new("4 线程", 4),
        new("8 线程", 8),
    ];
}

public sealed record MemoryOptimizationIntervalOption(string Name, int Seconds)
{
    public static IReadOnlyList<MemoryOptimizationIntervalOption> All { get; } =
    [
        new("10 秒", 10),
        new("30 秒", 30),
        new("1 分钟", 60),
        new("5 分钟", 300),
        new("10 分钟", 600),
    ];
}

public sealed record HotkeyOption(string Name, int VirtualKey)
{
    public static IReadOnlyList<HotkeyOption> All { get; } = CreateOptions();

    private static IReadOnlyList<HotkeyOption> CreateOptions()
    {
        var options = new List<HotkeyOption>();

        for (var key = 1; key <= 24; key++)
        {
            options.Add(new HotkeyOption($"F{key}", 0x6F + key));
        }

        for (var key = 'A'; key <= 'Z'; key++)
        {
            options.Add(new HotkeyOption(key.ToString(), key));
        }

        for (var key = 0; key <= 9; key++)
        {
            options.Add(new HotkeyOption(key.ToString(), 0x30 + key));
        }

        options.AddRange(
        [
            new HotkeyOption("Insert", 0x2D),
            new HotkeyOption("Delete", 0x2E),
            new HotkeyOption("Home", 0x24),
            new HotkeyOption("End", 0x23),
            new HotkeyOption("Page Up", 0x21),
            new HotkeyOption("Page Down", 0x22),
            new HotkeyOption("Pause", 0x13),
            new HotkeyOption("Scroll Lock", 0x91),
        ]);

        return options;
    }
}
