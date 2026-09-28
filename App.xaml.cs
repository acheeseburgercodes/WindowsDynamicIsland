using System.Windows;
using System.Runtime.InteropServices;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Give this process its own shell identity before its first window is created.
        _ = SetCurrentProcessExplicitAppUserModelID(VirtualDesktopService.IslandAppId);
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
