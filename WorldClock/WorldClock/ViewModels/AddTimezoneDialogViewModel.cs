using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WorldClock.ViewModels
{
    /// <summary>
    /// ViewModel used solely within AddTimezoneDialog for search and selection state.
    /// </summary>
    public class AddTimezoneDialogViewModel : INotifyPropertyChanged
    {
        private string _searchText = string.Empty;
        private TimeZoneInfo _selectedTimezone;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FilteredTimeZones));
                }
            }
        }

        public TimeZoneInfo SelectedTimezone
        {
            get => _selectedTimezone;
            set { _selectedTimezone = value; OnPropertyChanged(); }
        }

        public IEnumerable<TimeZoneInfo> FilteredTimeZones
        {
            get
            {
                var all = TimeZoneInfo.GetSystemTimeZones();
                if (string.IsNullOrWhiteSpace(_searchText))
                    return all;
                var lower = _searchText.Trim().ToLowerInvariant();
                return all.Where(tz =>
                    tz.DisplayName.ToLowerInvariant().Contains(lower) ||
                    tz.StandardName.ToLowerInvariant().Contains(lower) ||
                    tz.Id.ToLowerInvariant().Contains(lower));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
