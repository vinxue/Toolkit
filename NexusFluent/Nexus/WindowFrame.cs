using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Nexus
{
    internal static class WindowFrame
    {
        /// <summary>
        /// How far a maximized WindowChrome window extends past its monitor's work area,
        /// in DIPs. Measured rather than taken from SystemParameters, which only reflects
        /// the primary monitor's DPI.
        /// </summary>
        public static Thickness GetMaximizedOverhang(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT bounds))
            {
                return SystemParameters.WindowResizeBorderThickness;
            }

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info))
            {
                return SystemParameters.WindowResizeBorderThickness;
            }

            RECT work = info.rcWork;
            DpiScale dpi = VisualTreeHelper.GetDpi(window);
            return new Thickness(
                Math.Max(0, work.Left - bounds.Left) / dpi.DpiScaleX,
                Math.Max(0, work.Top - bounds.Top) / dpi.DpiScaleY,
                Math.Max(0, bounds.Right - work.Right) / dpi.DpiScaleX,
                Math.Max(0, bounds.Bottom - work.Bottom) / dpi.DpiScaleY);
        }

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }
}
