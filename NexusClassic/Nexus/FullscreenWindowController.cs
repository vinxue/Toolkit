using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Nexus
{
    internal sealed class FullscreenWindowController
    {
        private WindowFullscreenState? _state;

        public void Enter(Window window, Brush? fullscreenBackground = null)
        {
            if (_state is not null)
            {
                return;
            }

            _state = new WindowFullscreenState(
                Bounds: GetWindowBoundsForRestore(window),
                WindowState: window.WindowState,
                WindowStyle: window.WindowStyle,
                ResizeMode: window.ResizeMode,
                Topmost: window.Topmost,
                Background: window.Background);

            var monitorBounds = GetMonitorBounds(window);
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.Topmost = true;
            if (fullscreenBackground is not null)
            {
                window.Background = fullscreenBackground;
            }

            window.Left = monitorBounds.Left;
            window.Top = monitorBounds.Top;
            window.Width = monitorBounds.Width;
            window.Height = monitorBounds.Height;
        }

        public void Exit(Window window)
        {
            if (_state is null)
            {
                return;
            }

            WindowFullscreenState state = _state;
            _state = null;

            window.Background = state.Background;
            window.WindowState = WindowState.Normal;
            window.WindowStyle = state.WindowStyle;
            window.ResizeMode = state.ResizeMode;
            window.Topmost = state.Topmost;
            window.Left = state.Bounds.Left;
            window.Top = state.Bounds.Top;
            window.Width = state.Bounds.Width;
            window.Height = state.Bounds.Height;
            window.WindowState = state.WindowState;
        }

        private static Rect GetMonitorBounds(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var monitor = MonitorApi.MonitorFromWindow(hwnd, MonitorApi.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MonitorApi.MonitorInfo
            {
                cbSize = Marshal.SizeOf<MonitorApi.MonitorInfo>()
            };

            if (!MonitorApi.GetMonitorInfo(monitor, ref monitorInfo))
            {
                return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            }

            var topLeft = PointFromDevice(window, monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top);
            var bottomRight = PointFromDevice(window, monitorInfo.rcMonitor.Right, monitorInfo.rcMonitor.Bottom);
            return new Rect(topLeft, bottomRight);
        }

        private static Point PointFromDevice(Visual visual, int x, int y)
        {
            var point = new Point(x, y);
            return PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformFromDevice.Transform(point) ?? point;
        }

        private static Rect GetWindowBoundsForRestore(Window window)
        {
            Rect bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;

            return IsValidWindowBounds(bounds)
                ? bounds
                : new Rect(window.Left, window.Top, Math.Max(window.ActualWidth, 1), Math.Max(window.ActualHeight, 1));
        }

        private static bool IsValidWindowBounds(Rect bounds) =>
            !bounds.IsEmpty &&
            IsFinite(bounds.Left) &&
            IsFinite(bounds.Top) &&
            IsFinite(bounds.Width) &&
            IsFinite(bounds.Height) &&
            bounds.Width > 0 &&
            bounds.Height > 0;

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private sealed record WindowFullscreenState(
            Rect Bounds,
            WindowState WindowState,
            WindowStyle WindowStyle,
            ResizeMode ResizeMode,
            bool Topmost,
            Brush? Background);

        private static class MonitorApi
        {
            public const int MONITOR_DEFAULTTONEAREST = 2;

            [StructLayout(LayoutKind.Sequential)]
            public struct Rect
            {
                public int Left, Top, Right, Bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MonitorInfo
            {
                public int cbSize;
                public Rect rcMonitor;
                public Rect rcWork;
                public int dwFlags;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);
        }
    }
}
