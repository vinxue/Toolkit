using System.Windows;
using Nexus.Models;
using Nexus.Services;

namespace Nexus
{
    public partial class RemoveSiteWindow : Window
    {
        public bool DeleteProfileData { get; private set; }

        public RemoveSiteWindow(SiteConfig site)
        {
            InitializeComponent();
            MessageText.Text = $"Remove \"{site.Name}\" from the sidebar?";

            if (!SiteStore.HasSiteProfile(site))
            {
                DeleteProfileDataBox.Visibility = Visibility.Collapsed;
                return;
            }

            // A site switched back to the shared profile still owns the isolated data
            // it was using before, so the wording has to cover both cases.
            DeleteProfileDataBox.Content = SiteStore.UsesIsolatedProfile(site)
                ? "Delete this site's isolated profile: sign-ins, cookies and cache"
                : "Delete the isolated profile data this site left behind";
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            DeleteProfileData = DeleteProfileDataBox.IsChecked == true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
