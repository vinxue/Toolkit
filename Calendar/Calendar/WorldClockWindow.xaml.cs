using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CalendarApp.Core;

namespace CalendarApp
{
    public partial class WorldClockWindow : Window
    {
        private static readonly ReadOnlyCollection<TimeZoneInfo> SystemZones = TimeZoneInfo.GetSystemTimeZones();

        private readonly List<TimeZoneInfo> _activeZones;
        private readonly ObservableCollection<WorldClockItem> _items = [];
        private DateTime? _customLocalDateTime;
        private bool _updatingCalendar;

        public WorldClockWindow(List<TimeZoneInfo> activeZones)
        {
            InitializeComponent();
            _activeZones = activeZones;
            PickerHours.ItemsSource = Enumerable.Range(0, 24).ToArray();

            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var tz in _activeZones)
            {
                _items.Add(new WorldClockItem(tz, nowUtc));
            }

            ClockList.ItemsSource = _items;
            RefreshHeader(nowUtc);
        }

        public void UpdateClocks()
        {
            if (_customLocalDateTime is not null)
            {
                return;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            RefreshHeader(nowUtc);
            foreach (var item in _items)
            {
                item.Update(nowUtc, isLive: true);
            }
        }

        private DateTimeOffset GetReferenceUtc()
        {
            if (_customLocalDateTime is not DateTime custom)
            {
                return DateTimeOffset.UtcNow;
            }

            var unspecified = DateTime.SpecifyKind(custom, DateTimeKind.Unspecified);
            var offset = TimeZoneInfo.Local.GetUtcOffset(unspecified);
            return new DateTimeOffset(unspecified, offset).ToUniversalTime();
        }

        private void ApplyCustomDateTime(DateTime localDateTime)
        {
            _customLocalDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
            var refUtc = GetReferenceUtc();
            RefreshHeader(refUtc);
            foreach (var item in _items)
            {
                item.Update(refUtc, isLive: false);
            }
        }

        private void RefreshHeader(DateTimeOffset refUtc)
        {
            var local = refUtc.ToLocalTime();
            SelectedDateText.Text = local.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            SelectedTimeText.Text = local.ToString("HH:mm", CultureInfo.InvariantCulture);
            PopupTimeText.Text = SelectedTimeText.Text;
            NowButton.IsEnabled = _customLocalDateTime is not null;
        }

        private DateTime GetBaseLocalDateTime()
        {
            if (_customLocalDateTime is DateTime custom)
            {
                return custom;
            }

            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute >= 30 ? 30 : 0, 0);
        }

        private void DatePickerButton_Click(object sender, RoutedEventArgs e)
        {
            var baseDt = GetBaseLocalDateTime();
            _updatingCalendar = true;
            PopupCalendar.DisplayMode = CalendarMode.Month;
            PopupCalendar.SelectedDate = baseDt.Date;
            PopupCalendar.DisplayDate = baseDt.Date;
            _updatingCalendar = false;
            DatePickerPopup.IsOpen = true;
        }

        private void DatePickerButton_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            DatePickerPopup.IsOpen = false;
            ApplyCustomDateTime(GetBaseLocalDateTime().AddDays(e.Delta > 0 ? -1 : 1));
            e.Handled = true;
        }

        private void PopupCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingCalendar || PopupCalendar.SelectedDate is not DateTime selectedDate)
            {
                return;
            }

            CommitCalendarDate(selectedDate);
        }

        private void PopupCalendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!DatePickerPopup.IsOpen)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source && FindDayButton(source) is { DataContext: DateTime clickedDate })
            {
                if (PopupCalendar.SelectedDate == clickedDate)
                {
                    CommitCalendarDate(clickedDate);
                }
            }
        }

        private void CommitCalendarDate(DateTime selectedDate)
        {
            // Release mouse capture that WPF's Calendar control takes on click.
            if (Mouse.Captured is System.Windows.Controls.Calendar or CalendarItem)
            {
                Mouse.Capture(null);
            }

            DatePickerPopup.IsOpen = false;
            var baseDt = GetBaseLocalDateTime();
            ApplyCustomDateTime(new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, baseDt.Hour, baseDt.Minute, 0));
        }

        private static CalendarDayButton? FindDayButton(DependencyObject? current)
        {
            while (current is not null)
            {
                if (current is CalendarDayButton button)
                {
                    return button;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void TimePickerButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshHeader(GetReferenceUtc());
            TimePickerPopup.IsOpen = true;
        }

        private void TimePickerButton_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ShiftCustomMinutes(e.Delta > 0 ? -30 : 30);
            e.Handled = true;
        }

        private void TimeShiftMinus_Click(object sender, RoutedEventArgs e) => ShiftCustomMinutes(-30);

        private void TimeShiftPlus_Click(object sender, RoutedEventArgs e) => ShiftCustomMinutes(30);

        private void ShiftCustomMinutes(int deltaMinutes) =>
            ApplyCustomDateTime(GetBaseLocalDateTime().AddMinutes(deltaMinutes));

        private void PickerHour_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is int hour)
            {
                var baseDt = GetBaseLocalDateTime();
                TimePickerPopup.IsOpen = false;
                ApplyCustomDateTime(new DateTime(baseDt.Year, baseDt.Month, baseDt.Day, hour, 0, 0));
            }
        }

        private void NowButton_Click(object sender, RoutedEventArgs e)
        {
            _customLocalDateTime = null;
            UpdateClocks();
        }

        private void AddZoneButton_Click(object sender, RoutedEventArgs e)
        {
            var existingIds = _activeZones.Select(z => z.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            ZonePickerList.ItemsSource = SystemZones
                .Where(z => !existingIds.Contains(z.Id))
                .ToArray();
            ZonePickerPopup.IsOpen = true;
        }

        private void ZonePickerItem_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is not TimeZoneInfo tz)
            {
                return;
            }

            ZonePickerPopup.IsOpen = false;

            if (_activeZones.Any(z => string.Equals(z.Id, tz.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            int index = 0;
            while (index < _activeZones.Count && WorldClockItem.CompareZones(_activeZones[index], tz) <= 0)
            {
                index++;
            }

            _activeZones.Insert(index, tz);
            _items.Insert(index, new WorldClockItem(tz, GetReferenceUtc(), isLive: _customLocalDateTime is null));
        }

        private void RemoveZone_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is WorldClockItem item && item.CanRemove)
            {
                _items.Remove(item);
                _activeZones.RemoveAll(z => string.Equals(z.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !ZonePickerPopup.IsOpen && !TimePickerPopup.IsOpen && !DatePickerPopup.IsOpen)
            {
                Close();
                e.Handled = true;
            }
        }

        private void Content_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && WindowState == WindowState.Normal)
            {
                DragMove();
            }
        }
    }
}
