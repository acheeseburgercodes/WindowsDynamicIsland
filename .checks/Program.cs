using System.Runtime.InteropServices;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Services;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var dragPath = Path.GetFullPath("README.md");
        var dragData = new DataObject();
        dragData.SetFileDropList(new StringCollection { dragPath });
        Check(dragData.GetDataPresent(DataFormats.FileDrop), "standard Windows file drop format");
        Check(dragData.GetData(DataFormats.FileDrop) is string[] paths &&
              paths.Length == 1 && paths[0] == dragPath,
            "drop target receives the stored file path");
        var viewport = new Border { Width = 450, Height = 300, Background = Brushes.Black };
        var island = new Window { Width = 600, Height = 450, Left = 150, Top = 150,
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
            Content = viewport, ShowInTaskbar = false };
        var source = new Window { Width = 640, Height = 480, Content = new TextBox { Text = "Panel input test" } };
        var panel = new InteractiveAppPanelService();
        var service = new WindowService(island);
        service.Configure();
        try
        {
            island.Show();
            source.Show();
            island.UpdateLayout();
            Pump();
            var sourceHandle = new WindowInteropHelper(source).Handle;
            var islandHandle = new WindowInteropHelper(island).Handle;
            Check(panel.Attach(sourceHandle, viewport), "attach");
            Pump();
            var foreground = GetForegroundWindow();
            service.EnsureTopmost();
            panel.UpdateBounds(viewport);
            Pump();
            Check(GetForegroundWindow() == foreground, "stack refresh preserves keyboard focus");
            var position = viewport.PointToScreen(new Point(100, 100));
            var hit = WindowFromPoint(new NativePoint { X = (int)position.X, Y = (int)position.Y });
            Check(GetAncestor(hit, 2) == sourceHandle, "panel mouse hit reaches application HWND");
            Check((GetWindowLongPtr(islandHandle, -20).ToInt64() & 8) != 0, "island is topmost");
            panel.Suspend();
            Pump();
            Check(!IsWindowVisible(sourceHandle), "collapse hides app");
            service.EnsureTopmost();
            panel.UpdateBounds(viewport);
            Check(!IsWindowVisible(sourceHandle), "stack refresh does not reveal hidden app");
            panel.Resume(viewport);
            Pump();
            Check(IsWindowVisible(sourceHandle), "resume shows app in panel");
            panel.Detach();
            Check(IsWindowVisible(sourceHandle), "external action restores window");
            return 0;
        }
        finally { panel.Detach(); source.Close(); island.Close(); app.Shutdown(); }
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Check(bool result, string label)
    {
        Console.WriteLine($"{(result ? "PASS" : "FAIL")}: {label}");
        if (!result) throw new InvalidOperationException(label);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
}
