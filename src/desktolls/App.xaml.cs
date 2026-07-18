using System.IO;
using System.Threading;
using System.Windows;
using DeskTolls.Services;

namespace DeskTolls;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (WindowsUpdatePolicyService.TryGetElevatedCommand(e.Args, out var policyCommand))
        {
            Shutdown(WindowsUpdatePolicyService.ExecuteElevatedCommand(policyCommand));
            return;
        }

        if (e.Args.Any(argument =>
                string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(SelfTestRunner.Run() ? 0 : 1);
            return;
        }

        _singleInstanceMutex = new Mutex(true, @"Local\desktolls-single-instance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            NativeMethods.PostMessage(
                NativeMethods.HwndBroadcast,
                DeskTolls.MainWindow.ShowRequestMessage,
                IntPtr.Zero,
                IntPtr.Zero);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            WriteErrorLog(args.Exception);
            System.Windows.MessageBox.Show(
                $"desktolls 遇到错误：\n{args.Exception.Message}",
                "desktolls",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var startupLaunch = e.Args.Any(argument =>
            string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase));

        var window = new MainWindow(startupLaunch);
        MainWindow = window;
        SessionEnding += (_, _) => window.PrepareForSystemShutdown();

        window.Show();
        if (startupLaunch)
        {
            window.Hide();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void WriteErrorLog(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.SettingsDirectory);
            File.AppendAllText(
                Path.Combine(SettingsStore.SettingsDirectory, "desktolls-error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch
        {
            // Logging must never hide the original application error.
        }
    }
}
