using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WorldClock.ViewModels;
using WorldClock.Views;

namespace WorldClock
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private DispatcherTimer _headerTimer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _viewModel.Clocks.CollectionChanged += Clocks_CollectionChanged;
            UpdateClockCount();

            _headerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _headerTimer.Tick += (s, e) => UpdateHeaderTime();
            _headerTimer.Start();
            UpdateHeaderTime();

            Loaded += MainWindow_Loaded;

            InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => AddTimezone_Click(null, null)),
                new KeyGesture(Key.N, ModifierKeys.Control)));
        }

        #region Win11 Acrylic via DWM
        private static class DwmApi
        {
            public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
            public const int DWMSBT_TRANSIENTWINDOW   = 3;  // Acrylic

            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Request Acrylic system backdrop (Win11 22H2+; silently ignored on older OS)
            int backdropType = DwmApi.DWMSBT_TRANSIENTWINDOW;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType, Marshal.SizeOf<int>());

            // Make WPF's composition surface transparent so DWM acrylic shows through.
            // WindowChrome (GlassFrameThickness="-1") already called DwmExtendFrameIntoClientArea
            // and will re-apply it on every WM_ACTIVATE, so we don't need to call it here.
            var src = HwndSource.FromHwnd(hwnd);
            if (src?.CompositionTarget != null)
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
        #endregion

        private void UpdateHeaderTime()
        {
            var now = DateTime.Now;
            LocalTimeHeader.Text = $"Local time: {now:HH:mm:ss} \u2013 {now:dddd, MMMM d, yyyy}";
        }

        private void UpdateClockCount()
        {
            int count = _viewModel.Clocks.Count;
            ClockCountText.Text = count == 0
                ? "No timezones added. Click \"Add Timezone\" to get started."
                : $"{count} timezone{(count == 1 ? "" : "s")} displayed";

            StatusText.Text = count == 0 ? string.Empty : $"Showing {count} clock{(count == 1 ? "" : "s")}";
            EmptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Clocks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateClockCount();
        }

        private void AddTimezone_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddTimezoneDialog(_viewModel) { Owner = this };
            dialog.ShowDialog();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel?.Dispose();
            _headerTimer?.Stop();
        }
    }
}

