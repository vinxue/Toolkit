using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorldClock.Models
{
    /// <summary>
    /// Represents a single timezone clock entry with real-time DST-aware time display.
    /// </summary>
    public class ClockItemModel : INotifyPropertyChanged
    {
        private TimeZoneInfo _timeZoneInfo;
        private string _customLabel;
        private DateTime? _overrideUtc;

        public ClockItemModel(TimeZoneInfo timeZoneInfo, string customLabel = null)
        {
            _timeZoneInfo = timeZoneInfo ?? throw new ArgumentNullException(nameof(timeZoneInfo));
            _customLabel = customLabel;
        }

        public TimeZoneInfo TimeZoneInfo => _timeZoneInfo;

        /// <summary>
        /// When set by the Time Converter feature, all time displays show this UTC instant
        /// instead of live time. Null = live mode.
        /// </summary>
        public DateTime? OverrideUtc
        {
            get => _overrideUtc;
            set { _overrideUtc = value; RefreshTime(); }
        }

        /// <summary>Effective UTC reference: override if set, otherwise live now.</summary>
        private DateTime EffectiveUtc => _overrideUtc ?? DateTime.UtcNow;

        /// <summary>
        /// The IANA/Windows timezone ID used to identify this entry.
        /// </summary>
        public string TimeZoneId => _timeZoneInfo.Id;

        /// <summary>
        /// Display label: custom label if provided, otherwise timezone standard/display name.
        /// </summary>
        public string Label
        {
            get => string.IsNullOrWhiteSpace(_customLabel) ? _timeZoneInfo.DisplayName : _customLabel;
            set
            {
                if (_customLabel != value)
                {
                    _customLabel = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Short city or region name derived from the timezone display name.
        /// </summary>
        public string CityName
        {
            get
            {
                // Extract the city part after the UTC offset, e.g. "(UTC+08:00) Beijing, Chongqing..." -> "Beijing, Chongqing..."
                var displayName = _timeZoneInfo.DisplayName;
                var parenEnd = displayName.IndexOf(')', 0);
                if (parenEnd >= 0 && parenEnd + 2 < displayName.Length)
                    return displayName.Substring(parenEnd + 2).Trim();
                return displayName;
            }
        }

        /// <summary>
        /// The current local time in this timezone, DST-aware.
        /// </summary>
        public DateTime CurrentTime => TimeZoneInfo.ConvertTime(EffectiveUtc, _timeZoneInfo);

        /// <summary>
        /// Formatted time string for display (HH:mm:ss).
        /// </summary>
        public string TimeDisplay => CurrentTime.ToString("HH:mm:ss");

        /// <summary>
        /// Formatted date string for display.
        /// </summary>
        public string DateDisplay => CurrentTime.ToString("ddd, MMM dd");

        /// <summary>
        /// UTC offset string, e.g. "UTC+8:00" or "UTC-5:00".
        /// </summary>
        public string UtcOffsetDisplay
        {
            get
            {
                var offset = _timeZoneInfo.GetUtcOffset(EffectiveUtc);
                string sign = offset < TimeSpan.Zero ? "-" : "+";
                return $"UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
            }
        }

        /// <summary>
        /// Whether DST is currently active in this timezone.
        /// </summary>
        public bool IsDaylightSaving => _timeZoneInfo.IsDaylightSavingTime(EffectiveUtc);

        /// <summary>
        /// DST indicator text.
        /// </summary>
        public string DstIndicator => IsDaylightSaving ? "DST" : string.Empty;

        /// <summary>
        /// True if DST is active (used for visibility binding).
        /// </summary>
        public bool ShowDstBadge => IsDaylightSaving;

        /// <summary>
        /// Difference in hours relative to local machine time.
        /// </summary>
        public string RelativeOffset
        {
            get
            {
                var localOffset = TimeZoneInfo.Local.GetUtcOffset(EffectiveUtc);
                var thisOffset = _timeZoneInfo.GetUtcOffset(EffectiveUtc);
                var diff = thisOffset - localOffset;
                if (diff == TimeSpan.Zero) return "Local";
                string sign = diff > TimeSpan.Zero ? "+" : "-";
                var abs = diff.Duration();
                if (abs.Minutes == 0)
                    return $"{sign}{(int)abs.TotalHours}h";
                return $"{sign}{(int)abs.TotalHours}h {abs.Minutes}m";
            }
        }

        /// <summary>
        /// Notify all time-related properties to refresh on timer tick.
        /// </summary>
        public void RefreshTime()
        {
            OnPropertyChanged(nameof(CurrentTime));
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(DateDisplay));
            OnPropertyChanged(nameof(UtcOffsetDisplay));
            OnPropertyChanged(nameof(IsDaylightSaving));
            OnPropertyChanged(nameof(DstIndicator));
            OnPropertyChanged(nameof(ShowDstBadge));
            OnPropertyChanged(nameof(RelativeOffset));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
