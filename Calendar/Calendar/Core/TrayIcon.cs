using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalendarApp.Core
{
    // Notification-area icon via Shell_NotifyIcon, using the owner window's HWND for callbacks.
    public sealed class TrayIcon : IDisposable
    {
        private const int CallbackMessage = 0x8000 + 1;
        private const int NinSelect = 0x400;
        private const int NinKeySelect = 0x401;
        private const int WmContextMenu = 0x7B;
        private const uint IconId = 1;

        private readonly IntPtr _hwnd;
        private readonly HwndSource _source;
        private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        private readonly IntPtr _icon;
        private readonly string _tip;
        private bool _disposed;

        public bool IsAdded { get; private set; }

        public event Action? Selected;

        // Raised when the icon cannot be re-added after the shell restarts.
        public event Action? RegistrationFailed;

        // Screen coordinates in physical pixels.
        public event Action<int, int>? ContextRequested;

        public TrayIcon(Window owner, string tip)
        {
            _tip = tip;
            _hwnd = new WindowInteropHelper(owner).EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(WndProc);

            int size = GetSystemMetricsForDpi(49, GetDpiForWindow(_hwnd));
            PrivateExtractIcons(Environment.ProcessPath!, 0, size, size, out _icon, out _, 1, 0);
            Add();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _source.RemoveHook(WndProc);
            if (IsAdded)
            {
                var data = NewData(0);
                Shell_NotifyIcon(NimDelete, ref data);
                IsAdded = false;
            }

            if (_icon != IntPtr.Zero)
            {
                DestroyIcon(_icon);
            }
        }

        private bool Add()
        {
            var data = NewData(NifMessage | NifIcon | NifTip | NifShowTip);
            IsAdded = Shell_NotifyIcon(NimAdd, ref data);
            if (IsAdded)
            {
                // Without version 4 the callback uses legacy messages this class does not handle.
                data.uVersion = 4;
                if (!Shell_NotifyIcon(NimSetVersion, ref data))
                {
                    Shell_NotifyIcon(NimDelete, ref data);
                    IsAdded = false;
                }
            }

            return IsAdded;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == CallbackMessage)
            {
                switch ((int)(lParam.ToInt64() & 0xFFFF))
                {
                    case NinSelect:
                    case NinKeySelect:
                        Selected?.Invoke();
                        handled = true;
                        break;
                    case WmContextMenu:
                        long w = wParam.ToInt64();
                        ContextRequested?.Invoke((short)(w & 0xFFFF), (short)((w >> 16) & 0xFFFF));
                        handled = true;
                        break;
                }
            }
            else if (msg == _taskbarCreated && !Add())
            {
                RegistrationFailed?.Invoke();
            }

            return IntPtr.Zero;
        }

        private NotifyIconData NewData(uint flags) => new()
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = IconId,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = _tip.Length > 127 ? _tip[..127] : _tip,
        };

        private const uint NifMessage = 1;
        private const uint NifIcon = 2;
        private const uint NifTip = 4;
        private const uint NifShowTip = 0x80;
        private const uint NimAdd = 0;
        private const uint NimDelete = 2;
        private const uint NimSetVersion = 4;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
        private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "PrivateExtractIconsW")]
        private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, out IntPtr icon, out uint id, uint count, uint flags);
    }
}
