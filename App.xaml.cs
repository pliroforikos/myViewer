using System.Runtime.InteropServices;
using System.Windows;
using myView.Services;

namespace myView;

public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--test", StringComparer.OrdinalIgnoreCase))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            bool success = await SelfTest.RunAllTestsAsync();
            Shutdown(success ? 0 : 1);
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
