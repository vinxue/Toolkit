using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TimeTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer dispatcherTimer;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            // Setup update timer
            dispatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            dispatcherTimer.Tick += UpdateTimeDifference;
            dispatcherTimer.Start();

            // Set initial values to current time
            InitializeTimePickers();
            SetCurrentTime();
            UpdateTimeDifference(null, EventArgs.Empty);
        }

        public static class DwmApi
        {
            public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            int backdropType = 3;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));

            HwndSource mainWindowSrc = HwndSource.FromHwnd(hwnd);
            mainWindowSrc.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
            NonClientRegionAPI.MARGINS margins = new NonClientRegionAPI.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            NonClientRegionAPI.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        public static class NonClientRegionAPI
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;      // width of left border that retains its size
                public int cxRightWidth;     // width of right border that retains its size
                public int cyTopHeight;      // height of top border that retains its size
                public int cyBottomHeight;   // height of bottom border that retains its size
            };

            [DllImport("DwmApi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(
                IntPtr hwnd,
                ref MARGINS pMarInset);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void InitializeTimePickers()
        {
            hourCombo.ItemsSource = Enumerable.Range(0, 24).Select(i => i.ToString("D2"));
            minuteCombo.ItemsSource = Enumerable.Range(0, 60).Select(i => i.ToString("D2"));
            secondCombo.ItemsSource = Enumerable.Range(0, 60).Select(i => i.ToString("D2"));

            // Handle selection changes
            datePicker.SelectedDateChanged += (s, e) => UpdateTimeDifference();
            hourCombo.SelectionChanged += (s, e) => UpdateTimeDifference();
            minuteCombo.SelectionChanged += (s, e) => UpdateTimeDifference();
            secondCombo.SelectionChanged += (s, e) => UpdateTimeDifference();
        }

        private void SetCurrentTime()
        {
            var now = DateTime.Now;
            var initTime = "00";
            datePicker.SelectedDate = now.Date;
            hourCombo.SelectedItem = initTime;
            minuteCombo.SelectedItem = initTime;
            secondCombo.SelectedItem = initTime;
        }

        private void UpdateTimeDifference(object sender = null, EventArgs e = null)
        {
            try
            {
                var selectedDate = datePicker.SelectedDate ?? DateTime.Today;
                var hour = int.Parse(hourCombo.SelectedItem as string ?? "00");
                var minute = int.Parse(minuteCombo.SelectedItem as string ?? "00");
                var second = int.Parse(secondCombo.SelectedItem as string ?? "00");

                var targetTime = new DateTime(
                    selectedDate.Year, selectedDate.Month, selectedDate.Day,
                    hour, minute, second);

                var currentTime = DateTime.Now;
                var difference = targetTime - currentTime;

                FormatTimeDifference(difference);
            }
            catch (ArgumentOutOfRangeException)
            {
                daysText.Text = "Invalid";
                hoursText.Text = "date/time";
                minutesText.Text = "selected";
                secondsText.Text = string.Empty;
            }
        }

        private void FormatTimeDifference(TimeSpan difference)
        {
            var absolute = difference.Duration();

            daysText.Text = absolute.Days.ToString();
            hoursText.Text = absolute.Hours.ToString("D2");
            minutesText.Text = absolute.Minutes.ToString("D2");
            secondsText.Text = absolute.Seconds.ToString("D2");
        }
    }
}
