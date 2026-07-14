using System.ComponentModel;

namespace Nexus.Models
{
    /// <summary>
    /// Represents a single website hosted inside the app.
    /// </summary>
    public class SiteConfig : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _url = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Initial));
            }
        }

        public string Url
        {
            get => _url;
            set
            {
                if (_url == value) return;
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }

        /// <summary>
        /// Single uppercase letter used for the sidebar avatar.
        /// </summary>
        public string Initial =>
            string.IsNullOrWhiteSpace(_name) ? "?" : _name.Trim().Substring(0, 1).ToUpperInvariant();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
