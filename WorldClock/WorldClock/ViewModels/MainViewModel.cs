using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using WorldClock.Models;

namespace WorldClock.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DispatcherTimer _timer;

        // Time Converter state
        private bool _isConverterMode;
        private DateTime? _converterDate = DateTime.Today;
        private int _converterHour = DateTime.Now.Hour;
        private int _converterMinute = (DateTime.Now.Minute / 5) * 5;

        public ObservableCollection<ClockItemModel> Clocks { get; } = new ObservableCollection<ClockItemModel>();

        public ICommand RemoveClockCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ToggleConverterCommand { get; }
        public ICommand UseNowCommand { get; }

        // Hour / minute lists for the converter ComboBoxes
        public IReadOnlyList<int> HoursList { get; } = new List<int>(Enumerable.Range(0, 24));
        public IReadOnlyList<int> MinutesList { get; } = new List<int>(Enumerable.Range(0, 12).Select(i => i * 5));

        public MainViewModel()
        {
            RemoveClockCommand = new RelayCommand(RemoveClock);
            MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
            ToggleConverterCommand = new RelayCommand(_ => IsConverterMode = !IsConverterMode);
            UseNowCommand = new RelayCommand(_ => UseNow());

            // Default clocks: Local, UTC, US 4 zones, India, Israel
            AddTimezoneById(TimeZoneInfo.Local.Id);
            TryAddTimezone("UTC");
            TryAddTimezone("Eastern Standard Time");   // New York / Boston
            TryAddTimezone("Central Standard Time");   // Chicago / Houston
            TryAddTimezone("Mountain Standard Time");  // Denver / Phoenix
            TryAddTimezone("Pacific Standard Time");   // Los Angeles / Seattle
            TryAddTimezone("India Standard Time");     // Mumbai / Delhi
            TryAddTimezone("Israel Standard Time");    // Tel Aviv / Jerusalem

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        public ClockItemModel SelectedClock { get; set; }

        /// <summary>
        /// Adds a timezone clock by TimeZoneInfo.Id if not already present.
        /// </summary>
        public bool AddTimezoneById(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (Clocks.Any(c => c.TimeZoneId == id)) return false;
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                var newClock = new ClockItemModel(tz);
                if (_isConverterMode) ApplyOverrideToSingle(newClock);
                Clocks.Add(newClock);
                return true;
            }
            catch (TimeZoneNotFoundException) { return false; }
            catch (InvalidTimeZoneException) { return false; }
        }

        private void TryAddTimezone(string id)
        {
            // Don't duplicate local timezone
            if (id == TimeZoneInfo.Local.Id) return;
            try { AddTimezoneById(id); } catch { /* silently skip unavailable TZs */ }
        }

        private void RemoveClock(object parameter)
        {
            if (parameter is ClockItemModel item)
                Clocks.Remove(item);
        }

        private bool CanMoveUp(object parameter)
        {
            if (parameter is ClockItemModel item)
                return Clocks.IndexOf(item) > 0;
            return false;
        }

        private bool CanMoveDown(object parameter)
        {
            if (parameter is ClockItemModel item)
            {
                int idx = Clocks.IndexOf(item);
                return idx >= 0 && idx < Clocks.Count - 1;
            }
            return false;
        }

        private void MoveUp(object parameter)
        {
            if (parameter is ClockItemModel item)
            {
                int idx = Clocks.IndexOf(item);
                if (idx > 0) Clocks.Move(idx, idx - 1);
            }
        }

        private void MoveDown(object parameter)
        {
            if (parameter is ClockItemModel item)
            {
                int idx = Clocks.IndexOf(item);
                if (idx >= 0 && idx < Clocks.Count - 1) Clocks.Move(idx, idx + 1);
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            foreach (var clock in Clocks)
                clock.RefreshTime();
        }

        public void Dispose()
        {
            _timer?.Stop();
        }

        // ===== Time Converter =====

        public bool IsConverterMode
        {
            get => _isConverterMode;
            set
            {
                if (_isConverterMode != value)
                {
                    _isConverterMode = value;
                    OnPropertyChanged();
                    ApplyConverterTime();
                }
            }
        }

        public DateTime? ConverterDate
        {
            get => _converterDate;
            set
            {
                _converterDate = value ?? DateTime.Today;
                OnPropertyChanged();
                if (_isConverterMode) ApplyConverterTime();
            }
        }

        public int ConverterHour
        {
            get => _converterHour;
            set { _converterHour = value; OnPropertyChanged(); if (_isConverterMode) ApplyConverterTime(); }
        }

        public int ConverterMinute
        {
            get => _converterMinute;
            set { _converterMinute = value; OnPropertyChanged(); if (_isConverterMode) ApplyConverterTime(); }
        }

        private void UseNow()
        {
            var now = DateTime.Now;
            _converterDate = now.Date;
            _converterHour = now.Hour;
            _converterMinute = (now.Minute / 5) * 5;
            OnPropertyChanged(nameof(ConverterDate));
            OnPropertyChanged(nameof(ConverterHour));
            OnPropertyChanged(nameof(ConverterMinute));
            if (_isConverterMode) ApplyConverterTime();
        }

        private void ApplyConverterTime()
        {
            if (_isConverterMode)
            {
                var date = _converterDate ?? DateTime.Today;
                var localDt = new DateTime(date.Year, date.Month, date.Day,
                    _converterHour, _converterMinute, 0, DateTimeKind.Local);
                DateTime utc;
                try { utc = localDt.ToUniversalTime(); }
                catch { utc = DateTime.UtcNow; }
                foreach (var c in Clocks) c.OverrideUtc = utc;
            }
            else
            {
                foreach (var c in Clocks) c.OverrideUtc = null;
            }
        }

        private void ApplyOverrideToSingle(ClockItemModel clock)
        {
            var date = _converterDate ?? DateTime.Today;
            var localDt = new DateTime(date.Year, date.Month, date.Day,
                _converterHour, _converterMinute, 0, DateTimeKind.Local);
            try { clock.OverrideUtc = localDt.ToUniversalTime(); }
            catch { clock.OverrideUtc = DateTime.UtcNow; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
