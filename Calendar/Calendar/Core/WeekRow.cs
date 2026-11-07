using System.ComponentModel;

namespace Calendar.Core
{
    public sealed class WeekRow(int number) : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Number { get; } = number;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
