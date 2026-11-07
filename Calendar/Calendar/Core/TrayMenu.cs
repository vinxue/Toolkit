using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace Calendar.Core
{
    // Shows a WPF ContextMenu at a tray position; a 1px foreground host window lets it close on click-away.
    public static class TrayMenu
    {
        private static Window? _host;

        public static void Show(ContextMenu menu, int x, int y)
        {
            var host = _host ??= CreateHost();
            host.Show();
            var hwnd = new WindowInteropHelper(host).Handle;

            var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            GetMonitorInfo(MonitorFromPoint(new Point32 { X = x, Y = y }, 2), ref info);
            var work = info.rcWork;
            int px = Math.Clamp(x, work.Left, work.Right - 1);
            int py = Math.Clamp(y, work.Top, work.Bottom - 1);

            SetWindowPos(hwnd, new IntPtr(-1), px, py, 1, 1, 0x10);
            SetForegroundWindow(hwnd);

            menu.PlacementTarget = host;
            menu.Placement = py > (work.Top + work.Bottom) / 2 ? PlacementMode.Top : PlacementMode.Bottom;
            menu.Closed -= OnMenuClosed;
            menu.Closed += OnMenuClosed;
            menu.IsOpen = true;
        }

        private static void OnMenuClosed(object sender, RoutedEventArgs e) => _host?.Hide();

        private static Window CreateHost() => new()
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Width = 1,
            Height = 1,
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct Point32
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect32
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public Rect32 rcMonitor;
            public Rect32 rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point32 pt, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
    }
}
