using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Nexus.Models;
using Nexus.Services;

namespace Nexus
{
    public partial class SettingsWindow : Window
    {
        private readonly ObservableCollection<SiteConfig> _sites;
        private readonly Func<SiteConfig, string?> _addSite;
        private readonly Func<SiteConfig, string, string, SiteProfileMode, string?> _editSite;
        private readonly Func<SiteConfig, bool, bool> _removeSite;
        private readonly Func<CoreWebView2BrowsingDataKinds, Task> _clearSharedBrowsingDataAsync;
        private readonly Func<SiteConfig, CoreWebView2BrowsingDataKinds, Task> _clearSiteBrowsingDataAsync;
        private SiteConfig? _editingSite;

        public SettingsWindow(
            ObservableCollection<SiteConfig> sites,
            Func<SiteConfig, string?> addSite,
            Func<SiteConfig, string, string, SiteProfileMode, string?> editSite,
            Func<SiteConfig, bool, bool> removeSite,
            Func<CoreWebView2BrowsingDataKinds, Task> clearSharedBrowsingDataAsync,
            Func<SiteConfig, CoreWebView2BrowsingDataKinds, Task> clearSiteBrowsingDataAsync)
        {
            _sites = sites;
            _addSite = addSite;
            _editSite = editSite;
            _removeSite = removeSite;
            _clearSharedBrowsingDataAsync = clearSharedBrowsingDataAsync;
            _clearSiteBrowsingDataAsync = clearSiteBrowsingDataAsync;

            InitializeComponent();
            SitesList.ItemsSource = _sites;
            DataFolderText.Text = SiteStore.DataFolder;
            InitializeAboutInfo();
        }

        private void AddSite_Click(object sender, RoutedEventArgs e)
        {
            ResetSiteForm();
            ShowSiteForm();
        }

        private void ShowSiteForm()
        {
            SiteFormCard.Visibility = Visibility.Visible;
            NameBox.Focus();
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SiteStore.DataFolder);
                Process.Start(new ProcessStartInfo(SiteStore.DataFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open the data folder.\n{ex.Message}", "Data folder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeAboutInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            string appName = assemblyName.Name ?? "Nexus";
            string appVersion = versionInfo.ProductVersion ?? "Unknown";
            string webView2SdkVersion = typeof(CoreWebView2Environment).Assembly.GetName().Version?.ToString() ?? "Unknown";
            string webView2RuntimeVersion;
            int startYear = 2026;
            int currentYear = DateTime.Now.Year;
            string copyright = currentYear > startYear
                ? $"Copyright © {startYear} - {currentYear} Gavin Xue"
                : $"Copyright © {startYear} Gavin Xue";

            try
            {
                webView2RuntimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch
            {
                webView2RuntimeVersion = "Unavailable";
            }

            AppNameText.Text = appName;
            AppVersionText.Text = $"Version: {appVersion}";
            DotNetVersionText.Text = $".NET: {Environment.Version}";
            WebView2VersionText.Text = $"WebView2: SDK {webView2SdkVersion}, Runtime {webView2RuntimeVersion}";
            CopyrightText.Text = versionInfo.LegalCopyright ?? copyright;
        }

        private void SitesNavButton_Click(object sender, RoutedEventArgs e) =>
            ShowPage(SitesPage);

        private void ProfilesNavButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshProfilesPage();
            ShowPage(ProfilesPage);
        }

        private void AboutNavButton_Click(object sender, RoutedEventArgs e) =>
            ShowPage(AboutPage);

        private void ShowPage(UIElement visiblePage)
        {
            SitesPage.Visibility = ReferenceEquals(visiblePage, SitesPage) ? Visibility.Visible : Visibility.Collapsed;
            ProfilesPage.Visibility = ReferenceEquals(visiblePage, ProfilesPage) ? Visibility.Visible : Visibility.Collapsed;
            AboutPage.Visibility = ReferenceEquals(visiblePage, AboutPage) ? Visibility.Visible : Visibility.Collapsed;
            SitesNavButton.Tag = ReferenceEquals(visiblePage, SitesPage) ? "Selected" : null;
            ProfilesNavButton.Tag = ReferenceEquals(visiblePage, ProfilesPage) ? "Selected" : null;
            AboutNavButton.Tag = ReferenceEquals(visiblePage, AboutPage) ? "Selected" : null;
        }

        private void RefreshProfilesPage()
        {
            int sharedCount = _sites.Count(site => !site.UsesIsolatedProfile);
            SharedProfileUsageText.Text = sharedCount == 1
                ? "Used by 1 site, temporary pages and new windows."
                : $"Used by {sharedCount} sites, temporary pages and new windows.";

            var isolated = _sites.Where(site => site.UsesIsolatedProfile).ToList();
            IsolatedProfilesList.ItemsSource = isolated;
            IsolatedProfilesCard.Visibility = isolated.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SaveSite_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string url = UrlBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowAddError("Please enter a name.");
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowAddError("Please enter a valid http or https URL.");
                return;
            }

            string? error;
            SiteProfileMode profileMode = UseIsolatedProfileBox.IsChecked == true
                ? SiteProfileMode.Isolated
                : SiteProfileMode.Shared;

            if (_editingSite is not null)
            {
                if (_editingSite.ProfileMode != profileMode && !ConfirmProfileModeChange(profileMode))
                {
                    return;
                }

                error = _editSite(_editingSite, name, uri.AbsoluteUri, profileMode);
            }
            else
            {
                var site = new SiteConfig
                {
                    Name = name,
                    Url = uri.AbsoluteUri,
                    ProfileMode = profileMode
                };
                error = _addSite(site);
            }

            if (error is not null)
            {
                ShowAddError(error);
                return;
            }

            ResetSiteForm();
        }

        private void EditSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: SiteConfig site })
            {
                return;
            }

            _editingSite = site;
            SiteFormTitle.Text = "Edit site";
            SaveSiteButton.Content = "Save";
            UseIsolatedProfileBox.IsChecked = site.UsesIsolatedProfile;
            NameBox.Text = site.Name;
            UrlBox.Text = site.Url;
            AddErrorText.Visibility = Visibility.Collapsed;
            ShowSiteForm();
            NameBox.SelectAll();
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e) =>
            ResetSiteForm();

        private bool ConfirmProfileModeChange(SiteProfileMode profileMode)
        {
            string message = profileMode == SiteProfileMode.Isolated
                ? "Move this site to an isolated profile?\n\nIt will reload and you may need to sign in again. Its previous isolated session, if any, is reused."
                : "Move this site back to the shared profile?\n\nIt will reload with the shared session. Its isolated data is kept and reused if you switch back.";

            return MessageBox.Show(this, message, "Change profile", MessageBoxButton.OKCancel, MessageBoxImage.Question)
                == MessageBoxResult.OK;
        }

        private async void ClearSiteData_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: SiteConfig site })
            {
                return;
            }

            await ClearProfileDataAsync($"\"{site.Name}\"", kinds => _clearSiteBrowsingDataAsync(site, kinds));
        }

        private void ResetSiteForm()
        {
            _editingSite = null;
            SiteFormCard.Visibility = Visibility.Collapsed;
            SiteFormTitle.Text = "Add site";
            SaveSiteButton.Content = "Add site";
            UseIsolatedProfileBox.IsChecked = false;
            AddErrorText.Visibility = Visibility.Collapsed;
            NameBox.Clear();
            UrlBox.Text = "https://";
        }

        private void RemoveSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: SiteConfig site })
            {
                return;
            }

            var dialog = new RemoveSiteWindow(site) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _removeSite(site, dialog.DeleteProfileData);
            }
        }

        private async void ClearSharedData_Click(object sender, RoutedEventArgs e) =>
            await ClearProfileDataAsync("the shared profile", _clearSharedBrowsingDataAsync);

        /// <summary>
        /// Both Clear buttons act on the data types ticked at the top of the page, so
        /// the confirmation and reporting live in one place.
        /// </summary>
        private async Task ClearProfileDataAsync(string target, Func<CoreWebView2BrowsingDataKinds, Task> clearAsync)
        {
            CoreWebView2BrowsingDataKinds kinds = GetSelectedBrowsingDataKinds();
            if (kinds == 0)
            {
                MessageBox.Show(this, "Select at least one type of data to clear.", "Profiles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IncludesSignOutRisk(kinds))
            {
                string selectedData = string.Join(Environment.NewLine, GetSelectedBrowsingDataDescriptions());
                var result = MessageBox.Show(
                    this,
                    $"Clear the selected data for {target}?{Environment.NewLine}{Environment.NewLine}{selectedData}{Environment.NewLine}{Environment.NewLine}This may sign you out or remove local website data.",
                    "Clear browsing data",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.OK)
                {
                    return;
                }
            }

            try
            {
                await clearAsync(kinds);
                MessageBox.Show(this, "Selected browsing data cleared.", "Profiles", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not clear selected browsing data.\n{ex.Message}", "Profiles", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private CoreWebView2BrowsingDataKinds GetSelectedBrowsingDataKinds()
        {
            CoreWebView2BrowsingDataKinds kinds = 0;

            if (ClearCacheBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.DiskCache;
                kinds |= CoreWebView2BrowsingDataKinds.CacheStorage;
            }

            if (ClearCookiesBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.Cookies;
            }

            if (ClearSiteStorageBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.AllDomStorage;
                kinds |= CoreWebView2BrowsingDataKinds.ServiceWorkers;
            }

            if (ClearBrowsingHistoryBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.BrowsingHistory;
            }

            if (ClearDownloadHistoryBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.DownloadHistory;
            }

            if (ClearAutofillBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.GeneralAutofill;
                kinds |= CoreWebView2BrowsingDataKinds.PasswordAutosave;
            }

            if (ClearSiteSettingsBox.IsChecked == true)
            {
                kinds |= CoreWebView2BrowsingDataKinds.Settings;
            }

            return kinds;
        }

        private IEnumerable<string> GetSelectedBrowsingDataDescriptions()
        {
            if (ClearCacheBox.IsChecked == true)
            {
                yield return "- Cached images and files";
            }

            if (ClearCookiesBox.IsChecked == true)
            {
                yield return "- Cookies and sign-in data";
            }

            if (ClearSiteStorageBox.IsChecked == true)
            {
                yield return "- Site storage";
            }

            if (ClearBrowsingHistoryBox.IsChecked == true)
            {
                yield return "- Browsing history";
            }

            if (ClearDownloadHistoryBox.IsChecked == true)
            {
                yield return "- Download history";
            }

            if (ClearAutofillBox.IsChecked == true)
            {
                yield return "- Autofill and saved passwords";
            }

            if (ClearSiteSettingsBox.IsChecked == true)
            {
                yield return "- Site settings";
            }
        }

        private static bool IncludesSignOutRisk(CoreWebView2BrowsingDataKinds kinds) =>
            kinds != (CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);

        private void ShowAddError(string message)
        {
            AddErrorText.Text = message;
            AddErrorText.Visibility = Visibility.Visible;
        }
    }
}
