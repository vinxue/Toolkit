using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;

namespace Nexus.Controls
{
    /// <summary>
    /// Minimize / maximize / close buttons for a <see cref="WindowChrome"/> window whose
    /// frame is extended over the whole client area (required for Mica).
    /// </summary>
    /// <remarks>
    /// The maximize button answers WM_NCHITTEST with HTMAXBUTTON so Windows 11 shows Snap
    /// Layouts. The pointer is then in the non-client area, so WPF never sees it and the
    /// hover / pressed / click behavior of that button is reproduced from window messages.
    /// </remarks>
    public partial class CaptionButtons : UserControl
    {
        public static readonly DependencyProperty IsNonClientHoverProperty =
            DependencyProperty.RegisterAttached("IsNonClientHover", typeof(bool), typeof(CaptionButtons), new PropertyMetadata(false));

        public static readonly DependencyProperty IsNonClientPressedProperty =
            DependencyProperty.RegisterAttached("IsNonClientPressed", typeof(bool), typeof(CaptionButtons), new PropertyMetadata(false));

        public static bool GetIsNonClientHover(DependencyObject element) => (bool)element.GetValue(IsNonClientHoverProperty);

        public static void SetIsNonClientHover(DependencyObject element, bool value) => element.SetValue(IsNonClientHoverProperty, value);

        public static bool GetIsNonClientPressed(DependencyObject element) => (bool)element.GetValue(IsNonClientPressedProperty);

        public static void SetIsNonClientPressed(DependencyObject element, bool value) => element.SetValue(IsNonClientPressedProperty, value);

        private Window? _window;
        private HwndSource? _hwndSource;
        private bool _isMaximizeHover;
        private bool _isMaximizePressed;
        private bool _isSystemMenuStyleAllowed;

        public CaptionButtons()
        {
            InitializeComponent();
            PresentationSource.AddSourceChangedHandler(this, OnSourceChanged);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // Runs before the window is first shown, so DWM never paints its own buttons.
        private void OnSourceChanged(object sender, SourceChangedEventArgs e)
        {
            if (e.NewSource is HwndSource source)
            {
                RemoveSystemMenuStyle(source.Handle);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_hwndSource is not null)
            {
                return;
            }

            _window = Window.GetWindow(this);
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (_window is null || _hwndSource is null)
            {
                return;
            }

            // WindowChrome hooks in at SourceInitialized and hooks run last-added first,
            // so adding this one at Loaded lets it answer WM_NCHITTEST before WindowChrome.
            _hwndSource.AddHook(WndProc);
            RemoveSystemMenuStyle(_hwndSource.Handle);

            _window.StateChanged += Window_StateChanged;
            UpdateMaximizeButton();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;

            if (_window is not null)
            {
                _window.StateChanged -= Window_StateChanged;
                _window = null;
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            SetMaximizeVisualState(hover: false, pressed: false);
            UpdateMaximizeButton();
        }

        private void UpdateMaximizeButton()
        {
            if (_window is null)
            {
                return;
            }

            bool maximized = _window.WindowState == WindowState.Maximized;
            MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
            AutomationProperties.SetName(MaximizeButton, maximized ? "Restore" : "Maximize");
        }

        private static bool CanResize(Window window) =>
            window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_window is not null)
            {
                SystemCommands.MinimizeWindow(_window);
            }
        }

        // Only reached through UI Automation: mouse input on this button is non-client.
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_window is not null)
            {
                SystemCommands.CloseWindow(_window);
            }
        }

        private void ToggleMaximize()
        {
            if (_window is null)
            {
                return;
            }

            if (_window.WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(_window);
            }
            else
            {
                SystemCommands.MaximizeWindow(_window);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                // WPF puts WS_SYSMENU back whenever WindowStyle or ResizeMode changes.
                case WM_STYLECHANGING when (int)(long)wParam == GWL_STYLE && !_isSystemMenuStyleAllowed:
                    var styles = Marshal.PtrToStructure<STYLESTRUCT>(lParam);
                    if ((styles.styleNew & WS_SYSMENU) != 0)
                    {
                        styles.styleNew &= ~WS_SYSMENU;
                        Marshal.StructureToPtr(styles, lParam, false);
                    }
                    break;

                case WM_NCHITTEST:
                    int hit = HitTest(PointFromLParam(lParam));
                    if (hit != HTNOWHERE)
                    {
                        handled = true;
                        return (IntPtr)hit;
                    }
                    break;

                case WM_NCMOUSEMOVE:
                    bool overMaximize = (int)(long)wParam == HTMAXBUTTON;
                    if (overMaximize && !_isMaximizeHover)
                    {
                        TrackNonClientMouseLeave(hwnd);
                    }
                    SetMaximizeVisualState(overMaximize, pressed: false);
                    break;

                case WM_NCMOUSELEAVE:
                    if (!_isMaximizePressed)
                    {
                        SetMaximizeVisualState(hover: false, pressed: false);
                    }
                    break;

                // Left to DefWindowProc, these start its legacy caption-button tracking loop.
                // Double-clicks arrive here regardless of CS_DBLCLKS.
                case WM_NCLBUTTONDOWN or WM_NCLBUTTONDBLCLK when (int)(long)wParam == HTMAXBUTTON:
                    SetCapture(hwnd);
                    SetMaximizeVisualState(hover: true, pressed: true);
                    handled = true;
                    break;

                // While captured, pointer input arrives as client messages; like a native
                // button, the press follows the pointer and only a release on it counts.
                case WM_MOUSEMOVE when _isMaximizePressed:
                    SetMaximizeVisualState(IsOverMaximizeButton(ClientPointToScreen(hwnd, lParam)), pressed: true);
                    handled = true;
                    break;

                case WM_LBUTTONUP when _isMaximizePressed:
                    bool released = IsOverMaximizeButton(ClientPointToScreen(hwnd, lParam));
                    SetMaximizeVisualState(released, pressed: false);
                    ReleaseCapture();
                    handled = true;
                    if (released)
                    {
                        ToggleMaximize();
                    }
                    break;

                case WM_CAPTURECHANGED when _isMaximizePressed:
                    SetMaximizeVisualState(hover: false, pressed: false);
                    break;

                case WM_MOUSEMOVE when _isMaximizeHover:
                    SetMaximizeVisualState(hover: false, pressed: false);
                    break;

                // Without WS_SYSMENU neither DefWindowProc nor WindowChrome can show the menu.
                case WM_SYSCOMMAND when ((int)(long)wParam & 0xFFF0) == SC_KEYMENU && (int)(long)lParam == ' ':
                    ShowSystemMenu(hwnd, GetKeyboardMenuAnchor(), TPM_LEFTBUTTON);
                    handled = true;
                    break;

                case WM_NCRBUTTONUP when (int)(long)wParam == HTCAPTION:
                    ShowSystemMenu(hwnd, PointFromLParam(lParam), TPM_RIGHTBUTTON);
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// WindowChrome treats these buttons as client area, which would also swallow the
        /// window's top and right resize borders where they overlap; those are restored here.
        /// </summary>
        private int HitTest(Point screenPoint)
        {
            if (_window is null || !IsVisible || !Contains(this, screenPoint))
            {
                return HTNOWHERE;
            }

            if (_window.WindowState == WindowState.Normal &&
                CanResize(_window) &&
                WindowChrome.GetWindowChrome(_window) is { } chrome)
            {
                Point point = _window.PointFromScreen(screenPoint);
                bool top = point.Y < chrome.ResizeBorderThickness.Top;
                bool right = point.X >= _window.ActualWidth - chrome.ResizeBorderThickness.Right;

                if (top && right) return HTTOPRIGHT;
                if (top) return HTTOP;
                if (right) return HTRIGHT;
            }

            return IsOverMaximizeButton(screenPoint) ? HTMAXBUTTON : HTNOWHERE;
        }

        private bool IsOverMaximizeButton(Point screenPoint) =>
            MaximizeButton.IsVisible && Contains(MaximizeButton, screenPoint);

        private static bool Contains(FrameworkElement element, Point screenPoint)
        {
            Point local = element.PointFromScreen(screenPoint);
            return local.X >= 0 && local.Y >= 0 && local.X < element.ActualWidth && local.Y < element.ActualHeight;
        }

        private void SetMaximizeVisualState(bool hover, bool pressed)
        {
            _isMaximizeHover = hover;
            _isMaximizePressed = pressed;
            SetIsNonClientHover(MaximizeButton, hover);
            SetIsNonClientPressed(MaximizeButton, hover && pressed);
        }

        private void ShowSystemMenu(IntPtr hwnd, Point screenPoint, uint buttonFlag)
        {
            if (_window is null)
            {
                return;
            }

            // GetSystemMenu returns null without WS_SYSMENU; restoring it only for the call,
            // without a frame change, keeps DWM from ever drawing its caption buttons.
            int style = GetWindowLong(hwnd, GWL_STYLE);
            _isSystemMenuStyleAllowed = true;
            SetWindowLong(hwnd, GWL_STYLE, style | WS_SYSMENU);
            IntPtr menu = GetSystemMenu(hwnd, false);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_SYSMENU);
            _isSystemMenuStyleAllowed = false;

            if (menu == IntPtr.Zero)
            {
                return;
            }

            bool maximized = _window.WindowState == WindowState.Maximized;
            bool resizable = CanResize(_window);
            EnableSystemMenuItem(menu, SC_RESTORE, maximized);
            EnableSystemMenuItem(menu, SC_MOVE, !maximized);
            EnableSystemMenuItem(menu, SC_SIZE, !maximized && resizable);
            EnableSystemMenuItem(menu, SC_MINIMIZE, _window.ResizeMode != ResizeMode.NoResize);
            EnableSystemMenuItem(menu, SC_MAXIMIZE, !maximized && resizable);

            int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | buttonFlag, (int)screenPoint.X, (int)screenPoint.Y, hwnd, IntPtr.Zero);
            if (command != 0)
            {
                PostMessage(hwnd, WM_SYSCOMMAND, (IntPtr)command, IntPtr.Zero);
            }
        }

        private static void EnableSystemMenuItem(IntPtr menu, int command, bool enabled) =>
            EnableMenuItem(menu, (uint)command, MF_BYCOMMAND | (enabled ? MF_ENABLED : MF_GRAYED));

        // Native windows open the Alt+Space menu at the visible top-left corner, below the caption.
        private Point GetKeyboardMenuAnchor()
        {
            if (_window?.Content is not UIElement root)
            {
                return PointToScreen(new Point(0, ActualHeight));
            }

            double captionBottom = TranslatePoint(new Point(0, ActualHeight), root).Y;
            return root.PointToScreen(new Point(0, captionBottom));
        }

        private static void RemoveSystemMenuStyle(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_STYLE);
            if ((style & WS_SYSMENU) == 0)
            {
                return;
            }

            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_SYSMENU);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        private static void TrackNonClientMouseLeave(IntPtr hwnd)
        {
            var track = new TRACKMOUSEEVENT
            {
                cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(),
                dwFlags = TME_LEAVE | TME_NONCLIENT,
                hwndTrack = hwnd
            };
            TrackMouseEvent(ref track);
        }

        private static Point PointFromLParam(IntPtr lParam)
        {
            long value = (long)lParam;
            return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        }

        private static Point ClientPointToScreen(IntPtr hwnd, IntPtr lParam)
        {
            Point client = PointFromLParam(lParam);
            var point = new POINT { X = (int)client.X, Y = (int)client.Y };
            ClientToScreen(hwnd, ref point);
            return new Point(point.X, point.Y);
        }

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x00080000;

        private const int WM_STYLECHANGING = 0x007C;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCMOUSEMOVE = 0x00A0;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_NCLBUTTONDBLCLK = 0x00A3;
        private const int WM_NCRBUTTONUP = 0x00A5;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_CAPTURECHANGED = 0x0215;
        private const int WM_NCMOUSELEAVE = 0x02A2;

        private const int HTNOWHERE = 0;
        private const int HTCAPTION = 2;
        private const int HTMAXBUTTON = 9;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPRIGHT = 14;

        private const int SC_SIZE = 0xF000;
        private const int SC_MOVE = 0xF010;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_KEYMENU = 0xF100;
        private const int SC_RESTORE = 0xF120;
        private const uint TPM_LEFTBUTTON = 0x0000;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint MF_BYCOMMAND = 0x0000;
        private const uint MF_ENABLED = 0x0000;
        private const uint MF_GRAYED = 0x0001;
        private const uint TME_LEAVE = 0x0002;
        private const uint TME_NONCLIENT = 0x0010;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [StructLayout(LayoutKind.Sequential)]
        private struct STYLESTRUCT
        {
            public int styleOld;
            public int styleNew;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TRACKMOUSEEVENT
        {
            public int cbSize;
            public uint dwFlags;
            public IntPtr hwndTrack;
            public uint dwHoverTime;
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern int EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
