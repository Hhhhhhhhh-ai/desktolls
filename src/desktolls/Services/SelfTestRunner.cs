using System.IO;
using System.Runtime.InteropServices;
using DeskTolls.Models;

namespace DeskTolls.Services;

internal static class SelfTestRunner
{
    internal static bool Run()
    {
        var results = new List<string>();
        var passed = true;

        Check("Windows 运行环境", OperatingSystem.IsWindows(), results, ref passed);
        Check("64 位进程", Environment.Is64BitProcess, results, ref passed);

        var normalizedSettings = new AppSettings
        {
            ClicksPerSecond = 999,
            AutoClickHotkey = -1,
            MemoryOptimizationIntervalSeconds = 999,
        };
        normalizedSettings.Normalize();
        Check("设置范围校正", normalizedSettings.ClicksPerSecond == 100, results, ref passed);
        Check("默认热键回退", normalizedSettings.AutoClickHotkey == 0x77, results, ref passed);
        Check(
            "内存优化间隔回退",
            normalizedSettings.MemoryOptimizationIntervalSeconds == 10,
            results,
            ref passed);

        var hotkeyValues = HotkeyOption.All.Select(option => option.VirtualKey).ToArray();
        Check("热键列表唯一", hotkeyValues.Distinct().Count() == hotkeyValues.Length, results, ref passed);
        Check("F8 热键存在", HotkeyOption.All.Any(option => option.Name == "F8" && option.VirtualKey == 0x77), results, ref passed);

        var segments = CustomDownloadService.CreateSegments(4 * 1024 * 1024, 4);
        Check(
            "下载分段连续且完整",
            segments.Count == 4
            && segments.First().Start == 0
            && segments.Last().End == 4 * 1024 * 1024 - 1
            && segments.Zip(segments.Skip(1)).All(pair => pair.First.End + 1 == pair.Second.Start),
            results,
            ref passed);
        Check(
            "下载文件名自动识别",
            CustomDownloadService.SuggestFileName("https://example.com/files/demo.zip?x=1") == "demo.zip",
            results,
            ref passed);
        Check(
            "文件类型与后缀识别",
            CustomDownloadService.GetExtensionFromContentType(
                "application/vnd.microsoft.portable-executable") == ".exe"
            && CustomDownloadService.DetectFileExtension([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) == ".png",
            results,
            ref passed);

        var expectedInputSize = Environment.Is64BitProcess ? 40 : 28;
        Check(
            "SendInput 结构大小",
            Marshal.SizeOf<NativeMethods.Input>() == expectedInputSize,
            results,
            ref passed);

        try
        {
            using var icon = TrayIconFactory.Create(false);
            Check("托盘图标生成", icon.Width > 0 && icon.Height > 0, results, ref passed);
        }
        catch (Exception exception)
        {
            Check($"托盘图标生成 ({exception.Message})", false, results, ref passed);
        }

        try
        {
            var desktopState = new DesktopIconService().AreIconsVisible();
            Check("Explorer 桌面层探测", desktopState.HasValue, results, ref passed);
        }
        catch (Exception exception)
        {
            Check($"Explorer 桌面层探测 ({exception.Message})", false, results, ref passed);
        }

        try
        {
            _ = new ClassicContextMenuService().IsEnabled();
            Check("经典菜单注册表读取", true, results, ref passed);
        }
        catch (Exception exception)
        {
            Check($"经典菜单注册表读取 ({exception.Message})", false, results, ref passed);
        }

        try
        {
            _ = WindowsUpdatePolicyService.GetState();
            var commandParsed = WindowsUpdatePolicyService.TryGetElevatedCommand(
                [WindowsUpdatePolicyService.CommandPrefix + "disable"],
                out var command);
            Check(
                "Windows 更新策略只读检测",
                commandParsed && command == "disable",
                results,
                ref passed);
        }
        catch (Exception exception)
        {
            Check($"Windows 更新策略只读检测 ({exception.Message})", false, results, ref passed);
        }

        try
        {
            using var memoryOptimizer = new MemoryOptimizationService(() => 10);
            var result = memoryOptimizer.OptimizeNowAsync().GetAwaiter().GetResult();
            Check(
                "仅当前进程工作集裁剪",
                result is { Success: true, WorkingSetAfter: > 0 },
                results,
                ref passed);
        }
        catch (Exception exception)
        {
            Check($"仅当前进程工作集裁剪 ({exception.Message})", false, results, ref passed);
        }

        var downloadTest = Task.Run(CustomDownloadSelfTest.RunAsync).GetAwaiter().GetResult();
        var downloadError = string.IsNullOrWhiteSpace(downloadTest.Error)
            ? string.Empty
            : $" ({downloadTest.Error})";
        Check(
            $"HTTP Range 多线程下载{downloadError}",
            downloadTest.MultiThreadPassed && downloadTest.RangeRequestCount >= 3,
            results,
            ref passed);
        Check(
            $"不支持 Range 自动回退单线程{downloadError}",
            downloadTest.SingleThreadFallbackPassed,
            results,
            ref passed);
        Check(
            $"错误后缀自动纠正{downloadError}",
            downloadTest.AutomaticFileNamePassed,
            results,
            ref passed);
        Check(
            $"取消下载清理临时文件{downloadError}",
            downloadTest.CancellationCleanupPassed,
            results,
            ref passed);

        var logPath = Path.Combine(AppContext.BaseDirectory, "desktolls-self-test.log");
        File.WriteAllLines(
            logPath,
            [
                $"desktolls self-test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Result: {(passed ? "PASS" : "FAIL")}",
                string.Empty,
                .. results,
            ]);

        return passed;
    }

    private static void Check(
        string name,
        bool condition,
        ICollection<string> results,
        ref bool passed)
    {
        results.Add($"[{(condition ? "PASS" : "FAIL")}] {name}");
        passed &= condition;
    }
}
