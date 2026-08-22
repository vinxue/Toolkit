using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using Nexus.Core;
using Nexus.Models;
using Nexus.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Nexus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const double CollapsedWidth = 52;
        private const double ExpandedWidth = 220;
        private const double PopupWidth = 1180;
        private const double PopupHeight = 720;
        private const int WmDpiChanged = 0x02E0;

        private readonly ObservableCollection<SiteConfig> _sites = new();
        private readonly Dictionary<SiteConfig, WebView2> _webViews = new();
        private readonly ProfileManager _profileManager = new(SiteStore.UserDataFolder);
        private readonly PopupWindowManager _popupWindowManager = new();
        private readonly FullscreenWindowController _appFullscreenController = new();
        private readonly FullscreenWindowController _webFullscreenController = new();

        private WebView2? _temporaryWebView;
        private WebView2? _fullscreenWebView;
        private SiteConfig? _siteBeforeTemporaryPage;

        private IntPtr _hwnd;
        private HwndSource? _source;

        private bool _isSidebarExpanded;
        private bool _isAppFullscreen;
        private bool _isTemporaryPageVisible;
        private bool _isWebContentFullscreen;
        private bool _isShuttingDown;
        private bool _isRecoveringFromBrowserFailure;
        private Point _dragStartPoint;
        private SiteConfig? _dragCandidate;
        private ChromeState? _chromeBeforeFullscreen;

        /// <summary>
        /// The site the user most recently asked to see. Since WebView2
        /// initialization is async and not cancellable, in-flight requests for a
        /// site the user has since navigated away from check this token before
        /// touching shared UI state, so a slow-to-load site can't "win" and
        /// reappear after the user has already switched to another one.
        /// </summary>
        private SiteConfig? _pendingSite;

        public MainWindow()
        {
            InitializeComponent();

            _profileManager.BrowserProcessFailed += (_, _) => RecoverFromBrowserProcessFailure();

            foreach (var site in SiteStore.Load())
            {
                FaviconService.LoadCached(site, SiteStore.FaviconFolder);
                _sites.Add(site);
            }

            SiteList.ItemsSource = _sites;
        }

        #region Win11 Acrylic via DWM
        private static class DwmApi
        {
            public const int DWMWA_CAPTION_COLOR = 35;
            public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
            public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        }

        // Without WindowChrome, DwmExtendFrameIntoClientArea must be called directly:
        // it tells DWM which client pixels participate in frame/backdrop compositing.
        // Keep this restricted to the acrylic sidebar. WebView2 is a native child HWND;
        // if the frame is extended across the whole client area, a maximized window can
        // draw the title bar over the top of the web content.
        private static class NonClientRegionApi
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct Margins
            {
                public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
            }

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            // Request the Acrylic system backdrop (Win11 22H2+; silently ignored on
            // older OS, which just keeps today's normal opaque window look). This
            // applies to the whole window (title bar + client area) regardless of
            // whether WindowChrome/a custom frame is used.
            int backdropType = DwmApi.DWMSBT_TRANSIENTWINDOW;
            DwmApi.DwmSetWindowAttribute(_hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType, Marshal.SizeOf<int>());

            // DWMWA_SYSTEMBACKDROP_TYPE is window-scoped, so the native title bar also
            // gets Acrylic. Override the caption with an opaque Windows 11 light color
            // so the title bar stays visually normal while the client sidebar remains
            // backed by Acrylic.
            int titleBarColor = ToColorRef(Color.FromRgb(0xEF, 0xF4, 0xF9));
            DwmApi.DwmSetWindowAttribute(_hwnd, DwmApi.DWMWA_CAPTION_COLOR,
                ref titleBarColor, Marshal.SizeOf<int>());

            UpdateExtendedFrame();

            // Make WPF's composition surface transparent so the DWM backdrop can show
            // through wherever our own content (the sidebar) is semi-transparent.
            _source = HwndSource.FromHwnd(_hwnd);
            if (_source?.CompositionTarget != null)
            {
                _source.CompositionTarget.BackgroundColor = Colors.Transparent;
                _source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmDpiChanged)
            {
                Dispatcher.BeginInvoke(UpdateExtendedFrame, System.Windows.Threading.DispatcherPriority.Loaded);
            }

            return IntPtr.Zero;
        }

        private void UpdateExtendedFrame()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            double dpiScaleX = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double sidebarWidth = _isWebContentFullscreen || SidebarPanel.Visibility != Visibility.Visible
                ? 0
                : SidebarPanel.Width;
            var margins = new NonClientRegionApi.Margins
            {
                cxLeftWidth = (int)Math.Ceiling(sidebarWidth * dpiScaleX),
                cxRightWidth = 0,
                cyTopHeight = 0,
                cyBottomHeight = 0
            };
            NonClientRegionApi.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        }

        private static int ToColorRef(Color color) =>
            color.R | (color.G << 8) | (color.B << 16);
        #endregion

        /// <summary>
        /// Whether the sidebar shows site names (expanded) or icons only (collapsed).
        /// </summary>
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                if (_isSidebarExpanded == value) return;
                _isSidebarExpanded = value;
                SidebarPanel.Width = value ? ExpandedWidth : CollapsedWidth;
                UpdateExtendedFrame();
                OnPropertyChanged(nameof(IsSidebarExpanded));
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            IsSidebarExpanded = !IsSidebarExpanded;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) =>
            HandleShortcut(e, defer: false);

        /// <summary>
        /// Shortcuts arriving from a WebView2 run after its native key handling
        /// returns, otherwise focus changes made here are swallowed.
        /// </summary>
        private void WebView_PreviewKeyDown(object sender, KeyEventArgs e) =>
            HandleShortcut(e, defer: true);

        private void HandleShortcut(KeyEventArgs e, bool defer)
        {
            if (e.Key == Key.F11)
            {
                ToggleAppFullscreen();
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            // Captured now: the action may run deferred, by which time Shift is released.
            var newWindowProfile = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                ? SiteStore.PrivateProfile
                : SiteStore.SharedProfile;

            Action? action = e.Key switch
            {
                Key.N when !_isWebContentFullscreen => () => _ = OpenNewWindowAsync(newWindowProfile),
                Key.L when !_isWebContentFullscreen => ShowTemporaryAddressBar,
                Key.W when CanCloseTemporaryPage() => () => _ = CloseTemporaryPageAsync(),
                _ => null
            };

            if (action is null)
            {
                return;
            }

            e.Handled = true;

            if (defer)
            {
                Dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        // An open address bar means the user is typing, so Ctrl+W must not pull the
        // page out from under them.
        private bool CanCloseTemporaryPage() =>
            _isTemporaryPageVisible &&
            TemporaryAddressBar.Visibility != Visibility.Visible &&
            (!_isWebContentFullscreen || ReferenceEquals(_fullscreenWebView, _temporaryWebView));

        private void ToggleAppFullscreen()
        {
            if (_isWebContentFullscreen)
            {
                return;
            }

            SetAppFullscreen(!_isAppFullscreen);
        }

        private void SetAppFullscreen(bool fullscreen)
        {
            if (_isAppFullscreen == fullscreen)
            {
                return;
            }

            _isAppFullscreen = fullscreen;
            SetChromeHidden(fullscreen, _appFullscreenController);
        }

        /// <summary>
        /// Both fullscreen modes hide the same chrome, and web-content fullscreen
        /// leaves app fullscreen first, so a single saved state covers both.
        /// </summary>
        private void SetChromeHidden(bool hidden, FullscreenWindowController controller)
        {
            if (hidden)
            {
                _chromeBeforeFullscreen = new ChromeState(
                    WebHost.Background,
                    SidebarPanel.Visibility,
                    TemporaryAddressBar.Visibility);

                SidebarPanel.Visibility = Visibility.Collapsed;
                TemporaryAddressBar.Visibility = Visibility.Collapsed;
                WebHost.Background = Brushes.Black;
                controller.Enter(this);
            }
            else
            {
                SidebarPanel.Visibility = _chromeBeforeFullscreen?.Sidebar ?? Visibility.Visible;
                TemporaryAddressBar.Visibility = _chromeBeforeFullscreen?.AddressBar ?? Visibility.Collapsed;
                WebHost.Background = _chromeBeforeFullscreen?.WebHostBackground ?? Brushes.White;
                _chromeBeforeFullscreen = null;
                controller.Exit(this);
            }

            UpdateExtendedFrame();
        }

        private readonly record struct ChromeState(Brush? WebHostBackground, Visibility Sidebar, Visibility AddressBar);

        private void ShowTemporaryAddressBar()
        {
            TemporaryAddressBar.Visibility = Visibility.Visible;
            TemporaryAddressBox.Text = GetCurrentAddress() ?? string.Empty;
            UpdateTemporaryAddressPlaceholderVisibility();
            TemporaryAddressBox.Focus();
            TemporaryAddressBox.SelectAll();
        }

        private string? GetCurrentAddress()
        {
            if (_isTemporaryPageVisible && _temporaryWebView?.Source is not null)
            {
                return _temporaryWebView.Source.AbsoluteUri;
            }

            if (SiteList.SelectedItem is SiteConfig site &&
                _webViews.TryGetValue(site, out var webView) &&
                webView.Source is not null)
            {
                return webView.Source.AbsoluteUri;
            }

            return (SiteList.SelectedItem as SiteConfig)?.Url;
        }

        private void TemporaryAddressBox_TextChanged(object sender, TextChangedEventArgs e) =>
            UpdateTemporaryAddressPlaceholderVisibility();

        private void UpdateTemporaryAddressPlaceholderVisibility()
        {
            TemporaryAddressPlaceholderText.Visibility = string.IsNullOrEmpty(TemporaryAddressBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void TemporaryAddressBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                TemporaryAddressBar.Visibility = Visibility.Collapsed;
                WebHost.Focus();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;

            if (!WebAddressHelper.TryNormalize(TemporaryAddressBox.Text, out var uri))
            {
                ShowError("Please enter a valid web address.");
                return;
            }

            TemporaryAddressBar.Visibility = Visibility.Collapsed;
            await OpenTemporaryUrlAsync(uri);
        }

        private async void SiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SiteList.SelectedItem is not SiteConfig site)
            {
                return;
            }

            await ShowSiteAsync(site);
        }

        private void SiteList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _dragCandidate = null;

            if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null)
            {
                return;
            }

            _dragCandidate = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as SiteConfig;
        }

        private void SiteList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is null)
            {
                return;
            }

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var draggedSite = _dragCandidate;
            _dragCandidate = null;
            DragDrop.DoDragDrop(SiteList, draggedSite, DragDropEffects.Move);
        }

        private void SiteList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(SiteConfig))
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void SiteList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(SiteConfig)) is not SiteConfig draggedSite)
            {
                return;
            }

            int oldIndex = _sites.IndexOf(draggedSite);
            if (oldIndex < 0)
            {
                return;
            }

            int insertIndex = _sites.Count;
            var targetItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (targetItem?.DataContext is SiteConfig targetSite)
            {
                int targetIndex = _sites.IndexOf(targetSite);
                if (targetIndex < 0)
                {
                    return;
                }

                bool insertAfter = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
                insertIndex = targetIndex + (insertAfter ? 1 : 0);
            }

            if (oldIndex < insertIndex)
            {
                insertIndex--;
            }

            insertIndex = Math.Clamp(insertIndex, 0, _sites.Count - 1);
            if (oldIndex == insertIndex)
            {
                return;
            }

            _sites.Move(oldIndex, insertIndex);
            SiteStore.Save(_sites);
            SiteList.SelectedItem = draggedSite;
            e.Handled = true;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(
                _sites,
                AddSiteFromSettings,
                EditSiteFromSettings,
                RemoveSiteFromSettings,
                ClearSharedProfileBrowsingDataAsync,
                ClearSiteBrowsingDataAsync)
            {
                Owner = this
            };

            settingsWindow.ShowDialog();
        }

        private string? AddSiteFromSettings(SiteConfig site)
        {
            string? error = ValidateSite(null, site.Name, site.Url, site.ProfileMode);
            if (error is not null)
            {
                return error;
            }

            SiteStore.EnsureProfileConfiguration(site, _sites);
            _sites.Add(site);
            SiteStore.Save(_sites);
            SiteList.SelectedItem = site;
            return null;
        }

        /// <summary>
        /// Isolated sites keep their own session, so the same URL twice is a valid
        /// way to run two accounts side by side; only shared sites really collide.
        /// </summary>
        private string? ValidateSite(SiteConfig? editedSite, string name, string url, SiteProfileMode profileMode)
        {
            if (_sites.Any(existing => !ReferenceEquals(existing, editedSite) &&
                string.Equals(existing.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return "A site with this name already exists.";
            }

            if (profileMode == SiteProfileMode.Shared &&
                _sites.Any(existing => !ReferenceEquals(existing, editedSite) &&
                    existing.ProfileMode == SiteProfileMode.Shared &&
                    NormalizeUrl(existing.Url) == NormalizeUrl(url)))
            {
                return "This URL is already in the sidebar on the shared profile. Use an isolated profile to add it for a second account.";
            }

            return null;
        }

        private string? EditSiteFromSettings(SiteConfig site, string name, string url, SiteProfileMode profileMode)
        {
            string? error = ValidateSite(site, name, url, profileMode);
            if (error is not null)
            {
                return error;
            }

            bool profileChanged = site.ProfileMode != profileMode;

            site.Name = name;
            site.Url = url;
            site.ProfileMode = profileMode;

            // The previous isolated profile name is kept so switching back to
            // isolated later reuses its data instead of starting a new session.
            SiteStore.EnsureProfileConfiguration(site, _sites);
            SiteStore.Save(_sites);

            if (profileChanged)
            {
                ReloadSiteProfile(site);
            }
            else if (_webViews.TryGetValue(site, out var webView))
            {
                webView.Source = new Uri(site.Url);
            }

            return null;
        }

        /// <summary>
        /// A control's profile is fixed when its CoreWebView2 is created, so a site
        /// that changed profile mode has to be torn down and rebuilt.
        /// </summary>
        private void ReloadSiteProfile(SiteConfig site)
        {
            if (_webViews.TryGetValue(site, out var webView))
            {
                ExitWebContentFullscreenIfOwnedBy(webView);
                WebHost.Children.Remove(webView);
                webView.Dispose();
                _webViews.Remove(site);
            }

            if (ReferenceEquals(SiteList.SelectedItem, site) && !_isTemporaryPageVisible)
            {
                _ = ShowSiteAsync(site);
            }
        }

        private bool RemoveSiteFromSettings(SiteConfig site, bool deleteProfileData)
        {
            if (!_sites.Contains(site))
            {
                return false;
            }

            // A site switched back to the shared profile keeps its old isolated
            // profile on disk, so removal offers to delete that too.
            bool deleteProfile = deleteProfileData && SiteStore.HasSiteProfile(site);
            string profileName = site.ProfileName;

            if (deleteProfile)
            {
                // A profile is only erased once every window using it has closed.
                _popupWindowManager.CloseForProfile(profileName);
            }

            if (_webViews.TryGetValue(site, out var webView))
            {
                ExitWebContentFullscreenIfOwnedBy(webView);

                var profile = webView.CoreWebView2?.Profile;
                if (deleteProfile && string.Equals(profile?.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteProfile(profile!);
                    deleteProfile = false;
                }

                WebHost.Children.Remove(webView);
                webView.Dispose();
                _webViews.Remove(site);
            }

            bool wasSelected = ReferenceEquals(SiteList.SelectedItem, site);
            _sites.Remove(site);
            SiteStore.Save(_sites);
            FaviconService.DeleteCached(site, SiteStore.FaviconFolder);

            if (deleteProfile)
            {
                _ = DeleteUnloadedProfileAsync(profileName);
            }

            if (wasSelected)
            {
                SiteList.SelectedItem = null;
                ErrorHint.Visibility = Visibility.Collapsed;
                LoadingHint.Visibility = Visibility.Collapsed;
                EmptyHint.Visibility = Visibility.Visible;
            }

            return true;
        }

        private void DeleteProfile(CoreWebView2Profile profile)
        {
            try
            {
                profile.Delete();
            }
            catch (Exception ex)
            {
                ShowProfileDeleteWarning(ex);
            }
        }

        /// <summary>
        /// Deleting a profile requires a live CoreWebView2 on it, so a site that was
        /// never opened in this session needs a throwaway control first.
        /// </summary>
        private async Task DeleteUnloadedProfileAsync(string profileName)
        {
            try
            {
                await RunOnProfileAsync(
                    new ProfileContext(profileName, IsInPrivate: false),
                    webView =>
                    {
                        webView.CoreWebView2.Profile.Delete();
                        return Task.CompletedTask;
                    });
            }
            catch (Exception ex)
            {
                ShowProfileDeleteWarning(ex);
            }
        }

        /// <summary>
        /// Runs work against a profile that no visible WebView2 is currently using.
        /// The control must be in the visual tree or initialization never completes.
        /// </summary>
        private async Task RunOnProfileAsync(ProfileContext profile, Func<WebView2, Task> action)
        {
            if (_isShuttingDown)
            {
                return;
            }

            var scratchWebView = new WebView2 { Visibility = Visibility.Hidden };
            WebHost.Children.Add(scratchWebView);

            try
            {
                await _profileManager.InitializeAsync(scratchWebView, profile);
                await action(scratchWebView);
            }
            finally
            {
                WebHost.Children.Remove(scratchWebView);
                scratchWebView.Dispose();
            }
        }

        private void ShowProfileDeleteWarning(Exception ex) =>
            MessageBox.Show(
                this,
                $"The site was removed, but its isolated profile data could not be deleted.\n{ex.Message}",
                "Remove site",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

        private static string NormalizeUrl(string url)
        {
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return url.Trim().TrimEnd('/').ToLowerInvariant();
            }

            string authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            return $"{uri.Scheme.ToLowerInvariant()}://{authority.ToLowerInvariant()}{uri.PathAndQuery.TrimEnd('/')}";
        }

        private async Task ClearSharedProfileBrowsingDataAsync(CoreWebView2BrowsingDataKinds kinds) =>
            await ClearProfileBrowsingDataAsync(SiteStore.SharedProfile, kinds);

        private async Task ClearSiteBrowsingDataAsync(SiteConfig site, CoreWebView2BrowsingDataKinds kinds) =>
            await ClearProfileBrowsingDataAsync(GetSiteProfile(site), kinds);

        private async Task ClearProfileBrowsingDataAsync(ProfileContext profile, CoreWebView2BrowsingDataKinds kinds)
        {
            var webView = GetWebViewForProfile(profile.Name);
            if (webView?.CoreWebView2 is not null)
            {
                await ProfileManager.ClearBrowsingDataAsync(webView, kinds);
            }
            else
            {
                await RunOnProfileAsync(profile, scratchWebView =>
                    ProfileManager.ClearBrowsingDataAsync(scratchWebView, kinds));
            }

            ReloadWebViewsOnProfile(profile.Name);
        }

        /// <summary>
        /// Loaded pages keep rendering their signed-in state until they navigate again,
        /// so anything still open on the cleared profile is reloaded.
        /// </summary>
        private void ReloadWebViewsOnProfile(string profileName)
        {
            foreach (var view in _webViews.Values)
            {
                if (view.CoreWebView2 is { } core &&
                    string.Equals(core.Profile.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    core.Reload();
                }
            }
        }

        /// <summary>
        /// Resolves a site's profile, persisting a newly generated profile name so the
        /// site keeps the same session on the next launch.
        /// </summary>
        private ProfileContext GetSiteProfile(SiteConfig site)
        {
            if (SiteStore.EnsureProfileConfiguration(site, _sites))
            {
                SiteStore.Save(_sites);
            }

            return SiteStore.GetProfileContext(site);
        }

        private WebView2? GetWebViewForProfile(string profileName)
        {
            foreach (var pair in _webViews)
            {
                if (string.Equals(pair.Value.CoreWebView2?.Profile.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Lazily creates (on first use) and displays the WebView2 for the given site.
        /// </summary>
        private async Task ShowSiteAsync(SiteConfig site)
        {
            _pendingSite = site;
            _siteBeforeTemporaryPage = site;
            _isTemporaryPageVisible = false;
            TemporaryAddressBar.Visibility = Visibility.Collapsed;

            // Nothing can bring the temporary page back once a site is shown, so it is
            // torn down rather than left running behind the site.
            DisposeTemporaryWebView();

            EmptyHint.Visibility = Visibility.Collapsed;
            ErrorHint.Visibility = Visibility.Collapsed;

            // Cleared up front: a still-running load for a site the user just left
            // must not leave its hint on top of the site now being shown.
            LoadingHint.Visibility = Visibility.Collapsed;

            // Hide all currently hosted web views.
            foreach (var view in _webViews.Values)
            {
                view.Visibility = Visibility.Collapsed;
            }

            if (!_webViews.TryGetValue(site, out var webView))
            {
                LoadingHint.Visibility = Visibility.Visible;

                // Add the control to the visual tree first: WebView2 needs a parent
                // HWND before EnsureCoreWebView2Async can complete, otherwise it hangs.
                webView = new WebView2 { Visibility = Visibility.Hidden };
                WebHost.Children.Add(webView);

                try
                {
                    await InitializeWebViewAsync(webView, site);
                }
                catch (Exception ex)
                {
                    WebHost.Children.Remove(webView);
                    webView.Dispose();
                    if (ReferenceEquals(_pendingSite, site))
                    {
                        ShowError($"Could not open \"{site.Name}\".\n{ex.Message}");
                    }
                    return;
                }

                if (!_sites.Contains(site))
                {
                    // The site was removed (deleted from the UI) while it was loading.
                    WebHost.Children.Remove(webView);
                    webView.Dispose();
                    return;
                }

                _webViews[site] = webView;

                if (ReferenceEquals(_pendingSite, site))
                {
                    LoadingHint.Visibility = Visibility.Collapsed;
                }
            }

            // The user may have already switched to a different site while this one
            // was (still) loading - leave it hidden rather than popping back over
            // whatever is now selected.
            if (!ReferenceEquals(_pendingSite, site))
            {
                return;
            }

            webView.Visibility = Visibility.Visible;
        }

        private void ShowError(string message)
        {
            LoadingHint.Visibility = Visibility.Collapsed;
            EmptyHint.Visibility = Visibility.Collapsed;
            ErrorHint.Text = message;
            ErrorHint.Visibility = Visibility.Visible;
        }

        private void SetWebContentFullscreen(bool fullscreen)
        {
            if (_isWebContentFullscreen == fullscreen)
            {
                return;
            }

            if (fullscreen && _isAppFullscreen)
            {
                SetAppFullscreen(false);
            }

            _isWebContentFullscreen = fullscreen;
            SetChromeHidden(fullscreen, _webFullscreenController);
        }

        private void WebView_ContainsFullScreenElementChanged(WebView2 webView)
        {
            bool containsFullscreenElement = webView.CoreWebView2.ContainsFullScreenElement;
            if (containsFullscreenElement)
            {
                if (webView.Visibility != Visibility.Visible)
                {
                    return;
                }

                _fullscreenWebView = webView;
                SetWebContentFullscreen(true);
                return;
            }

            if (!ReferenceEquals(_fullscreenWebView, webView))
            {
                return;
            }

            _fullscreenWebView = null;
            SetWebContentFullscreen(false);
        }

        private void ExitWebContentFullscreenIfOwnedBy(WebView2 webView)
        {
            if (!ReferenceEquals(_fullscreenWebView, webView))
            {
                return;
            }

            _fullscreenWebView = null;
            SetWebContentFullscreen(false);
        }

        private async Task OpenTemporaryUrlAsync(Uri uri)
        {
            if (!_isTemporaryPageVisible)
            {
                _siteBeforeTemporaryPage = SiteList.SelectedItem as SiteConfig;
            }

            _pendingSite = null;

            EmptyHint.Visibility = Visibility.Collapsed;
            ErrorHint.Visibility = Visibility.Collapsed;

            foreach (var view in _webViews.Values)
            {
                view.Visibility = Visibility.Collapsed;
            }

            if (_temporaryWebView is null)
            {
                LoadingHint.Visibility = Visibility.Visible;
                _temporaryWebView = new WebView2 { Visibility = Visibility.Hidden };
                WebHost.Children.Add(_temporaryWebView);

                try
                {
                    await InitializeTemporaryWebViewAsync(_temporaryWebView);
                }
                catch (Exception ex)
                {
                    WebHost.Children.Remove(_temporaryWebView);
                    _temporaryWebView.Dispose();
                    _temporaryWebView = null;
                    ShowError($"Could not open temporary page.\n{ex.Message}");
                    return;
                }

                LoadingHint.Visibility = Visibility.Collapsed;
            }

            _temporaryWebView.Visibility = Visibility.Visible;

            // Navigate rather than assigning Source: re-entering the address that is
            // already loaded must reload the page, and Source no-ops on equal values.
            _temporaryWebView.CoreWebView2.Navigate(uri.AbsoluteUri);
            _isTemporaryPageVisible = true;
        }

        private async Task CloseTemporaryPageAsync()
        {
            TemporaryAddressBar.Visibility = Visibility.Collapsed;
            _isTemporaryPageVisible = false;
            _pendingSite = null;

            DisposeTemporaryWebView();

            if (_siteBeforeTemporaryPage is not null && _sites.Contains(_siteBeforeTemporaryPage))
            {
                SiteList.SelectedItem = _siteBeforeTemporaryPage;
                await ShowSiteAsync(_siteBeforeTemporaryPage);
                RestoreHostKeyboardFocus();
                return;
            }

            LoadingHint.Visibility = Visibility.Collapsed;
            ErrorHint.Visibility = Visibility.Collapsed;
            EmptyHint.Visibility = Visibility.Visible;
            RestoreHostKeyboardFocus();
        }

        private void RestoreHostKeyboardFocus()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                KeyboardFocusHost.Focus();
                Keyboard.Focus(KeyboardFocusHost);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private async Task InitializeTemporaryWebViewAsync(WebView2 webView)
        {
            // Ad-hoc links are usually to services the shared sites are already signed
            // in to, so the temporary page must not start signed out.
            var profile = SiteStore.SharedProfile;

            await _profileManager.InitializeAsync(webView, profile);

            webView.PreviewKeyDown += WebView_PreviewKeyDown;

            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                WebView_ContainsFullScreenElementChanged(webView);

            webView.CoreWebView2.NewWindowRequested += (s, args) =>
                OpenWebViewPopupWindow(args, profile);
        }

        /// <summary>
        /// Initializes a WebView2 already attached to the visual tree with an isolated
        /// user-data profile and supports SSO/login popups, then navigates to the
        /// site's URL.
        /// </summary>
        private async Task InitializeWebViewAsync(WebView2 webView, SiteConfig site)
        {
            var profile = GetSiteProfile(site);

            await _profileManager.InitializeAsync(webView, profile);

            webView.PreviewKeyDown += WebView_PreviewKeyDown;

            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                WebView_ContainsFullScreenElementChanged(webView);

            webView.CoreWebView2.FaviconChanged += (_, _) =>
                _ = FaviconService.UpdateAsync(webView.CoreWebView2, site, SiteStore.FaviconFolder);

            // Some pages open required work in a separate browser window: SSO/login
            // flows are the common case, but target="_blank" links can use this too.
            webView.CoreWebView2.NewWindowRequested += (s, args) =>
                OpenWebViewPopupWindow(args, profile);

            webView.Source = new Uri(site.Url);
        }

        /// <summary>
        /// Opens popup windows requested by a hosted page in a separate WebView2
        /// window sharing the same profile, so sign-in flows and new-window links work.
        /// </summary>
        private void OpenWebViewPopupWindow(
            CoreWebView2NewWindowRequestedEventArgs args,
            ProfileContext profile)
        {
            var deferral = args.GetDeferral();
            var popupWindow = CreatePopupWindow(profile);

            // Show the window first so the WebView2 control has a parent HWND before
            // EnsureCoreWebView2Async is awaited - otherwise initialization hangs.
            popupWindow.Show();

            _ = InitializePopupWebViewAsync(popupWindow, args, deferral);
        }

        private PopupWebViewWindow CreatePopupWindow(ProfileContext profile)
        {
            var popupWindow = new PopupWebViewWindow(
                _profileManager,
                profile,
                OpenWebViewPopupWindow,
                OpenNewWindowAsync)
            {
                Width = PopupWidth,
                Height = PopupHeight
            };

            _popupWindowManager.Track(popupWindow, profile.Name);
            return popupWindow;
        }

        private async Task OpenNewWindowAsync(ProfileContext profile)
        {
            PopupWebViewWindow? popupWindow = null;

            try
            {
                popupWindow = CreatePopupWindow(profile);
                // Show the window first so the WebView2 control has a parent HWND before
                // EnsureCoreWebView2Async is awaited - otherwise initialization hangs.
                popupWindow.Show();

                await popupWindow.InitializeStandaloneAsync();
            }
            catch (Exception ex)
            {
                popupWindow?.Close();
                MessageBox.Show(
                    this,
                    $"Could not open a new window.\n{ex.Message}",
                    "Nexus",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task InitializePopupWebViewAsync(
            PopupWebViewWindow popupWindow,
            CoreWebView2NewWindowRequestedEventArgs args,
            CoreWebView2Deferral deferral)
        {
            try
            {
                await popupWindow.InitializeAsync(args);
            }
            catch
            {
                popupWindow.Close();
            }
            finally
            {
                deferral.Complete();
            }
        }

        /// <summary>
        /// All profiles share one browser process, so its death invalidates every
        /// WebView2 in the app: they are torn down and the visible site is rebuilt
        /// against a fresh environment.
        /// </summary>
        private void RecoverFromBrowserProcessFailure()
        {
            if (_isRecoveringFromBrowserFailure || _isShuttingDown)
            {
                return;
            }

            _isRecoveringFromBrowserFailure = true;

            // Deferred: the WebView2 objects cannot be disposed inside their own
            // ProcessFailed callback.
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    var site = SiteList.SelectedItem as SiteConfig;

                    _popupWindowManager.CloseAll();
                    ExitAnyWebContentFullscreen();
                    DisposeAllWebViews();

                    _isTemporaryPageVisible = false;
                    TemporaryAddressBar.Visibility = Visibility.Collapsed;
                    _profileManager.Reset();

                    if (site is not null && _sites.Contains(site))
                    {
                        await ShowSiteAsync(site);
                        return;
                    }

                    LoadingHint.Visibility = Visibility.Collapsed;
                    ErrorHint.Visibility = Visibility.Collapsed;
                    EmptyHint.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    ShowError($"The browser process stopped and could not be restarted.\n{ex.Message}");
                }
                finally
                {
                    _isRecoveringFromBrowserFailure = false;
                }
            }));
        }

        private void DisposeAllWebViews()
        {
            foreach (var view in _webViews.Values)
            {
                WebHost.Children.Remove(view);
                SafeDispose(view);
            }

            _webViews.Clear();
            DisposeTemporaryWebView();
        }

        private void DisposeTemporaryWebView()
        {
            if (_temporaryWebView is null)
            {
                return;
            }

            ExitWebContentFullscreenIfOwnedBy(_temporaryWebView);
            _temporaryWebView.PreviewKeyDown -= WebView_PreviewKeyDown;
            WebHost.Children.Remove(_temporaryWebView);
            SafeDispose(_temporaryWebView);
            _temporaryWebView = null;
        }

        /// <summary>
        /// After a browser process failure the underlying CoreWebView2 objects are
        /// already gone, so releasing them can fail with nothing left to clean up.
        /// </summary>
        private static void SafeDispose(WebView2 webView)
        {
            try
            {
                webView.Dispose();
            }
            catch (Exception)
            {
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child is not null)
            {
                if (child is T parent)
                {
                    return parent;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _isShuttingDown = true;
            base.OnClosing(e);
        }

        /// <summary>
        /// Disposes all cached WebView2 instances on shutdown so the browser process
        /// and its profile locks are released immediately.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _isShuttingDown = true;
            _source?.RemoveHook(WndProc);
            _source = null;

            if (_isAppFullscreen)
            {
                SetAppFullscreen(false);
            }

            ExitAnyWebContentFullscreen();

            _popupWindowManager.CloseAll();
            DisposeAllWebViews();
            _profileManager.Reset();
        }

        private void ExitAnyWebContentFullscreen()
        {
            if (_fullscreenWebView is null)
            {
                return;
            }

            _fullscreenWebView = null;
            SetWebContentFullscreen(false);
        }
    }
}
