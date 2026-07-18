using System.Diagnostics;

namespace DeskTolls.Services;

public static class ExplorerService
{
    public static async Task RestartAsync()
    {
        var explorerProcesses = Process.GetProcessesByName("explorer");

        foreach (var process in explorerProcesses)
        {
            using (process)
            {
                try
                {
                    process.Kill(false);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (InvalidOperationException)
                {
                    // Explorer already exited between enumeration and termination.
                }
                catch (TimeoutException)
                {
                    // Starting a replacement shell below remains the recovery path.
                }
            }
        }

        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
        });

        await Task.Delay(1400);
    }
}
