using System.Windows;
using Nexus.Models;

namespace Nexus
{
    /// <summary>
    /// Simple dialog to add a new website (name + URL).
    /// </summary>
    public partial class AddSiteWindow : Window
    {
        private readonly IReadOnlyCollection<SiteConfig> _existingSites;

        public SiteConfig? Result { get; private set; }

        public AddSiteWindow(IEnumerable<SiteConfig> existingSites)
        {
            _existingSites = existingSites.ToList();
            InitializeComponent();
            Loaded += (_, _) => NameBox.Focus();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string url = UrlBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowError("Please enter a name.");
                return;
            }

            if (_existingSites.Any(site => string.Equals(site.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError("A site with this name already exists.");
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowError("Please enter a valid http or https URL.");
                return;
            }

            if (_existingSites.Any(site => NormalizeUrl(site.Url) == NormalizeUrl(uri.AbsoluteUri)))
            {
                ShowError("This URL is already in the sidebar.");
                return;
            }

            Result = new SiteConfig { Name = name, Url = uri.AbsoluteUri };
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private static string NormalizeUrl(string url)
        {
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return url.Trim().TrimEnd('/').ToLowerInvariant();
            }

            string authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            return $"{uri.Scheme.ToLowerInvariant()}://{authority.ToLowerInvariant()}{uri.PathAndQuery.TrimEnd('/')}";
        }
    }
}
