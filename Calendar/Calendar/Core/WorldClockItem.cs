using System.ComponentModel;
using System.Globalization;

namespace CalendarApp.Core
{
    public sealed class WorldClockItem : INotifyPropertyChanged
    {
        private static readonly string[] DefaultZoneIds =
        [
            "Pacific Standard Time",
            "Mountain Standard Time",
            "Central Standard Time",
            "Eastern Standard Time",
            "UTC",
            "Central European Standard Time",
            "Israel Standard Time",
            "India Standard Time",
        ];

        private string _timeText = "";
        private string _subText = "";

        public WorldClockItem(TimeZoneInfo timeZone, DateTimeOffset nowUtc, bool isLive = true)
        {
            TimeZone = timeZone;
            IsLocal = string.Equals(timeZone.Id, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase);
            Update(nowUtc, isLive);
        }

        public TimeZoneInfo TimeZone { get; }

        public string Id => TimeZone.Id;

        public string DisplayName => TimeZone.DisplayName;

        public bool IsLocal { get; }

        public bool CanRemove => !IsLocal;

        public string TimeText
        {
            get => _timeText;
            private set
            {
                if (_timeText != value)
                {
                    _timeText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeText)));
                }
            }
        }

        public string SubText
        {
            get => _subText;
            private set
            {
                if (_subText != value)
                {
                    _subText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubText)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Update(DateTimeOffset nowUtc, bool isLive = true)
        {
            var target = TimeZoneInfo.ConvertTime(nowUtc, TimeZone);
            var local = nowUtc.ToLocalTime();

            TimeText = target.ToString(isLive ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);

            string datePart = target.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);
            if (IsLocal)
            {
                SubText = $"{datePart} · Local";
                return;
            }

            double hours = (target.Offset - local.Offset).TotalHours;
            string offsetPart = hours switch
            {
                > 0 => $"+{hours:0.#}h",
                < 0 => $"{hours:0.#}h",
                _ => "0h",
            };

            SubText = $"{datePart} · {offsetPart}";
        }

        public static List<TimeZoneInfo> CreateDefaultZones()
        {
            var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase)
            {
                [TimeZoneInfo.Local.Id] = TimeZoneInfo.Local,
            };

            foreach (string id in DefaultZoneIds)
            {
                if (!zones.ContainsKey(id) && TryFindZone(id, out var tz) && tz is not null)
                {
                    zones[tz.Id] = tz;
                }
            }

            var result = zones.Values.ToList();
            result.Sort(CompareZones);
            return result;
        }

        public static int CompareZones(TimeZoneInfo a, TimeZoneInfo b)
        {
            int cmp = a.BaseUtcOffset.CompareTo(b.BaseUtcOffset);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        }

        private static bool TryFindZone(string id, out TimeZoneInfo? zone)
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch
            {
                zone = null;
                return false;
            }
        }
    }
}
