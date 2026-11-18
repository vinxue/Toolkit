using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using CalendarApp.Core;

namespace CalendarApp
{
    public partial class MainWindow : Window
    {
        private static readonly DateOnly MinDate = new(1901, 1, 1);
        private static readonly DateOnly MaxDate = new(2100, 12, 31);
        private const double DetailPaneMinWindowWidth = 660;

        private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
        private DateOnly _selected;
        private DayInfo[] _days = [];
        private WeekRow[] _weeks = [];
        private DateOnly _gridStart;
        private int _year;
        private int _month;
        private int _pickerYear;
        private int _animGeneration;
        private bool _updatingSelection;
        private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly List<TimeZoneInfo> _worldTimeZones = WorldClockItem.CreateDefaultZones();
        private WorldClockWindow? _worldClockWindow;
        private TrayIcon? _tray;
        private bool _exiting;

        public MainWindow()
        {
            InitializeComponent();
            GoTo(_today, animate: false);

            Application.Current.SessionEnding += (_, _) => _exiting = true;

            _clock.Tick += (_, _) => UpdateClock();
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible)
                {
                    UpdateClock();
                    _clock.Start();
                }
                else
                {
                    _clock.Stop();
                }
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _tray = new TrayIcon(this, "Calendar");
            _tray.Selected += ShowFromTray;
            _tray.RegistrationFailed += ShowFromTray;
            _tray.ContextRequested += (x, y) => TrayMenu.Show((ContextMenu)FindResource("TrayMenu"), x, y);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_exiting && _tray is { IsAdded: true })
            {
                e.Cancel = true;
                _worldClockWindow?.Close();
                Hide();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _clock.Stop();
            _tray?.Dispose();
            base.OnClosed(e);
        }

        // Creates the HWND (and with it the tray icon) without showing the window.
        public void StartInTray()
        {
            new WindowInteropHelper(this).EnsureHandle();
            if (_tray is not { IsAdded: true })
            {
                Show();
            }
        }

        public void ShowFromTray()
        {
            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            // Activate can be refused by the foreground lock; raising Topmost briefly still lifts the window.
            if (!Activate() && !Topmost)
            {
                Topmost = true;
                Topmost = false;
            }
        }

        private void TogglePin()
        {
            Topmost = !Topmost;
            PinButton.Visibility = Topmost ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PinButton_Click(object sender, RoutedEventArgs e) => TogglePin();

        private void PinMenuItem_Click(object sender, RoutedEventArgs e) => TogglePin();

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MoreMenu.PlacementTarget = MoreButton;
            MoreMenu.Placement = PlacementMode.Bottom;
            MoreMenu.IsOpen = true;
        }

        private void MoreMenu_Opened(object sender, RoutedEventArgs e)
        {
            PinMenuItem.IsChecked = Topmost;
            AutoStartMenuItem.IsChecked = AutoStart.IsEnabled;

            string? otherPath = AutoStart.GetOtherRegisteredPath();
            AutoStartSuffixRun.Text = otherPath is null ? "" : " (other)";
            AutoStartMenuItem.ToolTip = otherPath is null
                ? null
                : $"Currently registered to:\n{otherPath}";
        }

        private void WorldClock_Click(object sender, RoutedEventArgs e)
        {
            if (_worldClockWindow is { IsLoaded: true })
            {
                if (_worldClockWindow.WindowState == WindowState.Minimized)
                {
                    _worldClockWindow.WindowState = WindowState.Normal;
                }

                _worldClockWindow.Activate();
                return;
            }

            _worldClockWindow = new WorldClockWindow(_worldTimeZones) { Owner = this };
            _worldClockWindow.Closed += (_, _) => _worldClockWindow = null;
            _worldClockWindow.Show();
        }

        private void TrayOpen_Click(object sender, RoutedEventArgs e) => ShowFromTray();

        private void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            if (!AutoStart.TrySetEnabled(item.IsChecked))
            {
                item.IsChecked = AutoStart.IsEnabled;
                MessageBox.Show(
                    this,
                    "Unable to update the startup setting in the Windows Registry.",
                    "Calendar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _exiting = true;
            _tray?.Dispose();
            Application.Current.Shutdown();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            string pattern = CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern;
            string timePattern = Regex.Replace(pattern, "t+", "").Trim();
            ClockText.Text = now.ToString(timePattern);
            ClockSuffix.Text = timePattern == pattern ? "" : now.ToString("tt");

            var (shichen, range) = GanZhi.Shichen(now);
            ClockShichen.Text = $"{shichen}时 · {range}";
            CompactClockText.Text = ClockText.Text;
            CompactClockSuffix.Text = ClockSuffix.Text;
            CompactShichen.Text = ClockShichen.Text;
            _worldClockWindow?.UpdateClocks();

            if (DateOnly.FromDateTime(now) != _today)
            {
                RefreshToday();
            }
        }

        private void GoTo(DateOnly date, bool animate = true)
        {
            date = date < MinDate ? MinDate : date > MaxDate ? MaxDate : date;
            _selected = date;

            bool monthChanged = _days.Length == 0 || date.Year != _year || date.Month != _month;
            int direction = 0;
            BitmapSource? outgoing = null;

            if (monthChanged)
            {
                direction = _days.Length == 0
                    ? 0
                    : Math.Sign((date.Year * 12 + date.Month) - (_year * 12 + _month));
                if (animate && direction != 0 && SystemParameters.ClientAreaAnimation)
                {
                    outgoing = CaptureViewport();
                }

                _year = date.Year;
                _month = date.Month;
                RebuildGrid();
            }

            _updatingSelection = true;
            DayList.SelectedItem = _days.First(d => d.Date == date);
            _updatingSelection = false;

            int selectedRow = (date.DayNumber - _gridStart.DayNumber) / 7;
            for (int i = 0; i < _weeks.Length; i++)
            {
                _weeks[i].IsSelected = i == selectedRow;
            }

            if (monthChanged && animate && direction != 0 && SystemParameters.ClientAreaAnimation)
            {
                AnimateMonthChange(direction, outgoing);
            }

            TitleText.Text = $"{_year}年{_month}月";
            TodayButton.IsEnabled = date != _today;
            UpdateDetail();
        }

        private void RebuildGrid()
        {
            var start = DayInfo.GridStart(_year, _month);
            _days = Enumerable.Range(0, DayInfo.GridRows * DayInfo.GridColumns)
                .Select(i => DayInfo.Create(start.AddDays(i), _month, _today))
                .ToArray();
            DayList.ItemsSource = _days;
            _gridStart = start;
            _weeks = Enumerable.Range(0, DayInfo.GridRows)
                .Select(i => new WeekRow(WeekNumber.Of(start.AddDays(i * 7))))
                .ToArray();
            WeekList.ItemsSource = _weeks;
        }

        private BitmapSource? CaptureViewport()
        {
            double width = BodyViewport.ActualWidth;
            double height = BodyViewport.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var dpi = VisualTreeHelper.GetDpi(BodyViewport);
            int pixelWidth = Math.Max(1, (int)Math.Round(width * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Round(height * dpi.DpiScaleY));

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(
                    new VisualBrush(BodyViewport)
                    {
                        Stretch = Stretch.None,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                        Viewbox = new Rect(0, 0, width, height),
                        ViewboxUnits = BrushMappingMode.Absolute
                    },
                    null,
                    new Rect(0, 0, width, height));
            }

            var bitmap = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                96.0 * dpi.DpiScaleX,
                96.0 * dpi.DpiScaleY,
                PixelFormats.Pbgra32);
            bitmap.Render(dv);
            bitmap.Freeze();
            return bitmap;
        }

        private void AnimateMonthChange(int direction, BitmapSource? outgoing)
        {
            int generation = ++_animGeneration;

            // Measure and arrange the new month's containers before starting the animation clock
            // so container generation doesn't eat the opening frames.
            BodyHost.UpdateLayout();

            if (outgoing is not null)
            {
                var outDuration = TimeSpan.FromMilliseconds(140);
                OutgoingImage.Source = outgoing;
                OutgoingShift.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(0, -direction * 14, outDuration)
                    {
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                    });

                var fadeOut = new DoubleAnimation(1, 0, outDuration);
                fadeOut.Completed += (_, _) =>
                {
                    if (generation == _animGeneration)
                    {
                        OutgoingImage.Source = null;
                    }
                };
                OutgoingImage.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                OutgoingImage.Source = null;
            }

            var inSlideDuration = TimeSpan.FromMilliseconds(240);
            var inFadeDuration = TimeSpan.FromMilliseconds(220);
            BodyShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(direction * 16, 0, inSlideDuration)
                {
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                });
            BodyHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, inFadeDuration)
                {
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void UpdateDetail()
        {
            var date = _selected;
            var lunar = ChineseCalendar.GetLunar(date);
            var festival = ChineseCalendar.GetFestival(date, lunar);
            var term = ChineseCalendar.GetSolarTerm(date);

            int diff = date.DayNumber - _today.DayNumber;
            string relative = diff == 0 ? "今天" : diff > 0 ? $"{diff} 天后" : $"{-diff} 天前";

            DetailDate.Text = $"{date.Year}年{date.Month}月{date.Day}日";
            DetailWeekday.Text = $"{DayInfo.WeekdayName(date)} · {relative}";

            DetailLunarDay.Text = lunar is null ? "—" : lunar.MonthName + lunar.DayName;
            DetailLunarYear.Text = lunar is null ? "" : $"{lunar.YearName}（{lunar.Zodiac}）";
            DetailLunarYear.Visibility = lunar is null ? Visibility.Collapsed : Visibility.Visible;

            var (ganzhiYear, ganzhiMonth) = GanZhi.YearAndMonth(date);
            DetailGanZhi.Text = $"{ganzhiYear}年 {ganzhiMonth}月 {GanZhi.Day(date)}日";

            SetRow(DetailFestivalLabel, DetailFestival, festival);
            SetRow(DetailSolarTermLabel, DetailSolarTerm, term);

            DetailWeek.Text = $"第 {WeekNumber.Of(date)} 周";
            DetailDayOfYear.Text = $"第 {date.DayOfYear} 天";
        }

        private static void SetRow(TextBlock label, TextBlock value, string? text)
        {
            value.Text = text ?? "";
            label.Visibility = value.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void DayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_updatingSelection && DayList.SelectedItem is DayInfo day)
            {
                GoTo(day.Date);
            }
        }

        private void Window_Activated(object? sender, EventArgs e) => RefreshToday();

        private void RefreshToday()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today != _today)
            {
                bool followToday = _selected == _today;
                _today = today;
                _days = [];
                GoTo(followToday ? today : _selected, animate: false);
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool showPane = e.NewSize.Width >= DetailPaneMinWindowWidth;
            DetailPane.Visibility = showPane ? Visibility.Visible : Visibility.Collapsed;
            CompactClock.Visibility = showPane ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Input in the open menu routes through the window; leave it to the menu.
            if (PickerPopup.IsOpen || MoreMenu.IsOpen || (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            switch (e.Key)
            {
                case Key.Left: GoTo(_selected.AddDays(-1)); break;
                case Key.Right: GoTo(_selected.AddDays(1)); break;
                case Key.Up: GoTo(_selected.AddDays(-7)); break;
                case Key.Down: GoTo(_selected.AddDays(7)); break;
                case Key.PageUp: GoTo(ctrl ? _selected.AddYears(-1) : _selected.AddMonths(-1)); break;
                case Key.PageDown: GoTo(ctrl ? _selected.AddYears(1) : _selected.AddMonths(1)); break;
                case Key.Home:
                case Key.T: GoTo(_today); break;
                case Key.P when ctrl: TogglePin(); break;
                default: return;
            }

            e.Handled = true;
        }

        private void CalendarGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PickerPopup.IsOpen || MoreMenu.IsOpen || e.Delta == 0)
            {
                return;
            }

            GoTo(_selected.AddMonths(e.Delta > 0 ? -1 : 1));
            e.Handled = true;
        }

        // Buttons and day cells handle the click themselves, so only empty areas start a drag.
        private void Content_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && WindowState == WindowState.Normal)
            {
                DragMove();
            }
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e) => GoTo(_today);

        private void PrevMonth_Click(object sender, RoutedEventArgs e) => GoTo(_selected.AddMonths(-1));

        private void NextMonth_Click(object sender, RoutedEventArgs e) => GoTo(_selected.AddMonths(1));

        private void TitleButton_Click(object sender, RoutedEventArgs e)
        {
            _pickerYear = _year;
            UpdatePicker();
            PickerPopup.IsOpen = true;
        }

        private void PickerPrevYear_Click(object sender, RoutedEventArgs e) => ShiftPickerYear(-1);

        private void PickerNextYear_Click(object sender, RoutedEventArgs e) => ShiftPickerYear(1);

        private void ShiftPickerYear(int delta)
        {
            _pickerYear = Math.Clamp(_pickerYear + delta, MinDate.Year, MaxDate.Year);
            UpdatePicker();
        }

        private void UpdatePicker()
        {
            PickerYearText.Text = $"{_pickerYear}年";
            PickerMonths.ItemsSource = Enumerable.Range(1, 12)
                .Select(m => new PickerItem(m, _pickerYear == _year && m == _month))
                .ToArray();
        }

        private void PickerMonth_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is int month)
            {
                int day = Math.Min(_selected.Day, DateTime.DaysInMonth(_pickerYear, month));
                PickerPopup.IsOpen = false;
                GoTo(new DateOnly(_pickerYear, month, day));
            }
        }
    }
}
