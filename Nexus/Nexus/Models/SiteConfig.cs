using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Nexus.Models
{
    /// <summary>
    /// Represents a single website hosted inside the app.
    /// </summary>
    public class SiteConfig : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = string.Empty;
        private string _profileName = string.Empty;
        private SiteProfileMode _profileMode = SiteProfileMode.Shared;
        private string _url = string.Empty;
        private ImageSource? _favicon;

        public string Id
        {
            get => _id;
            set
            {
                if (_id == value) return;
                _id = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
                OnPropertyChanged(nameof(Id));
            }
        }

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

        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName == value) return;
                _profileName = value;
                OnPropertyChanged(nameof(ProfileName));
            }
        }

        public SiteProfileMode ProfileMode
        {
            get => _profileMode;
            set
            {
                if (_profileMode == value) return;
                _profileMode = value;
                OnPropertyChanged(nameof(ProfileMode));
                OnPropertyChanged(nameof(UsesIsolatedProfile));
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

        /// <summary>
        /// Site icon shown in the sidebar. Cached on disk, never in the config file.
        /// </summary>
        [JsonIgnore]
        public ImageSource? Favicon
        {
            get => _favicon;
            set
            {
                if (ReferenceEquals(_favicon, value)) return;
                _favicon = value;
                OnPropertyChanged(nameof(Favicon));
            }
        }

        public bool UsesIsolatedProfile => _profileMode == SiteProfileMode.Isolated;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
