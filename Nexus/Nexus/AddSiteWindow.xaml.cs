using System.Windows;
using Nexus.Models;

namespace Nexus
{
    /// <summary>
    /// Simple dialog to add a new website (name + URL).
    /// </summary>
    public partial class AddSiteWindow : Window
    {
        public SiteConfig? Result { get; private set; }

        public AddSiteWindow()
        {
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

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowError("Please enter a valid http or https URL.");
                return;
            }

            Result = new SiteConfig { Name = name, Url = uri.ToString() };
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
    }
}
