using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
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
        private const double CollapsedWidth = 60;
        private const double ExpandedWidth = 220;
        private const double PopupWidth = 1180;
        private const double PopupHeight = 720;
        private const int WmDpiChanged = 0x02E0;

        private readonly ObservableCollection<SiteConfig> _sites = new();
        private readonly Dictionary<SiteConfig, WebView2> _webViews = new();
        private readonly HashSet<Window> _popupWindows = new();
        private readonly FullscreenWindowController _mainFullscreenController = new();

        private WebView2? _temporaryWebView;
        private CoreWebView2Environment? _temporaryEnvironment;
        private WebView2? _fullscreenWebView;
        private SiteConfig? _siteBeforeTemporaryPage;

        private IntPtr _hwnd;
        private HwndSource? _source;

        private bool _isSidebarExpanded;
        private bool _isTemporaryPageVisible;
        private bool _isWebContentFullscreen;
        private Point _dragStartPoint;
        private SiteConfig? _dragCandidate;
        private Brush? _webHostBackgroundBeforeFullscreen;
        private Visibility _sidebarVisibilityBeforeFullscreen;
        private Visibility _temporaryAddressBarVisibilityBeforeFullscreen;

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

            foreach (var site in SiteStore.Load())
            {
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

        private static class MonitorApi
        {
            public const int MONITOR_DEFAULTTONEAREST = 2;

            [StructLayout(LayoutKind.Sequential)]
            public struct Rect
            {
                public int Left, Top, Right, Bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MonitorInfo
            {
                public int cbSize;
                public Rect rcMonitor;
                public Rect rcWork;
                public int dwFlags;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);
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

        private static Rect GetMonitorBounds(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var monitor = MonitorApi.MonitorFromWindow(hwnd, MonitorApi.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MonitorApi.MonitorInfo
            {
                cbSize = Marshal.SizeOf<MonitorApi.MonitorInfo>()
            };

            if (!MonitorApi.GetMonitorInfo(monitor, ref monitorInfo))
            {
                return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            }

            var topLeft = PointFromDevice(window, monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top);
            var bottomRight = PointFromDevice(window, monitorInfo.rcMonitor.Right, monitorInfo.rcMonitor.Bottom);
            return new Rect(topLeft, bottomRight);
        }

        private static Point PointFromDevice(Visual visual, int x, int y)
        {
            var point = new Point(x, y);
            return PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformFromDevice.Transform(point) ?? point;
        }

        private static Rect GetWindowBoundsForRestore(Window window)
        {
            Rect bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;

            return IsValidWindowBounds(bounds)
                ? bounds
                : new Rect(window.Left, window.Top, Math.Max(window.ActualWidth, 1), Math.Max(window.ActualHeight, 1));
        }

        private static bool IsValidWindowBounds(Rect bounds) =>
            !bounds.IsEmpty &&
            IsFinite(bounds.Left) &&
            IsFinite(bounds.Top) &&
            IsFinite(bounds.Width) &&
            IsFinite(bounds.Height) &&
            bounds.Width > 0 &&
            bounds.Height > 0;

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private sealed class FullscreenWindowController
        {
            private WindowFullscreenState? _state;

            public void Enter(Window window, Brush? fullscreenBackground = null)
            {
                if (_state is not null)
                {
                    return;
                }

                _state = new WindowFullscreenState(
                    Bounds: GetWindowBoundsForRestore(window),
                    WindowState: window.WindowState,
                    WindowStyle: window.WindowStyle,
                    ResizeMode: window.ResizeMode,
                    Topmost: window.Topmost,
                    Background: window.Background);

                var monitorBounds = GetMonitorBounds(window);
                window.WindowState = WindowState.Normal;
                window.WindowStyle = WindowStyle.None;
                window.ResizeMode = ResizeMode.NoResize;
                window.Topmost = true;
                if (fullscreenBackground is not null)
                {
                    window.Background = fullscreenBackground;
                }

                window.Left = monitorBounds.Left;
                window.Top = monitorBounds.Top;
                window.Width = monitorBounds.Width;
                window.Height = monitorBounds.Height;
            }

            public void Exit(Window window)
            {
                if (_state is null)
                {
                    return;
                }

                WindowFullscreenState state = _state;
                _state = null;

                window.Background = state.Background;
                window.WindowState = WindowState.Normal;
                window.WindowStyle = state.WindowStyle;
                window.ResizeMode = state.ResizeMode;
                window.Topmost = state.Topmost;
                window.Left = state.Bounds.Left;
                window.Top = state.Bounds.Top;
                window.Width = state.Bounds.Width;
                window.Height = state.Bounds.Height;
                window.WindowState = state.WindowState;
            }

            private sealed record WindowFullscreenState(
                Rect Bounds,
                WindowState WindowState,
                WindowStyle WindowStyle,
                ResizeMode ResizeMode,
                bool Topmost,
                Brush? Background);
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            if (e.Key == Key.L)
            {
                if (_isWebContentFullscreen)
                {
                    return;
                }

                ShowTemporaryAddressBar();
                e.Handled = true;
            }
            else if (e.Key == Key.W && CanCloseTemporaryPage())
            {
                e.Handled = true;
                _ = CloseTemporaryPageAsync();
            }
        }

        private bool CanCloseTemporaryPage() =>
            _isTemporaryPageVisible &&
            (!_isWebContentFullscreen || ReferenceEquals(_fullscreenWebView, _temporaryWebView));

        private void ShowTemporaryAddressBar()
        {
            TemporaryAddressBar.Visibility = Visibility.Visible;
            TemporaryAddressBox.Text = _temporaryWebView?.Source?.AbsoluteUri ?? string.Empty;
            UpdateTemporaryAddressPlaceholderVisibility();
            TemporaryAddressBox.Focus();
            TemporaryAddressBox.SelectAll();
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

            if (!TryNormalizeWebAddress(TemporaryAddressBox.Text, out var uri))
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

        private void AddSiteButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSiteWindow(_sites) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is not null)
            {
                SiteStore.EnsureProfileFolderName(dialog.Result);
                _sites.Add(dialog.Result);
                SiteStore.Save(_sites);
                SiteList.SelectedItem = dialog.Result;
            }
        }

        private void DeleteSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: SiteConfig site })
            {
                return;
            }

            var result = MessageBox.Show(
                this,
                $"Remove \"{site.Name}\" and its saved sign-in data?",
                "Remove site",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (_webViews.TryGetValue(site, out var webView))
            {
                ExitWebContentFullscreenIfOwnedBy(webView);
                WebHost.Children.Remove(webView);
                webView.Dispose();
                _webViews.Remove(site);
            }

            bool wasSelected = ReferenceEquals(SiteList.SelectedItem, site);
            _sites.Remove(site);
            SiteStore.Save(_sites);
            SiteStore.DeleteProfileFolder(site);

            if (wasSelected)
            {
                SiteList.SelectedItem = null;
                ErrorHint.Visibility = Visibility.Collapsed;
                LoadingHint.Visibility = Visibility.Collapsed;
                EmptyHint.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Lazily creates (on first use) and displays the WebView2 for the given site.
        /// </summary>
        private async Task ShowSiteAsync(SiteConfig site)
        {
            _pendingSite = site;
            _siteBeforeTemporaryPage = site;
            _isTemporaryPageVisible = false;

            if (_temporaryWebView is not null)
            {
                _temporaryWebView.Visibility = Visibility.Collapsed;
            }

            EmptyHint.Visibility = Visibility.Collapsed;
            ErrorHint.Visibility = Visibility.Collapsed;

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

            _isWebContentFullscreen = fullscreen;

            if (fullscreen)
            {
                _webHostBackgroundBeforeFullscreen = WebHost.Background;
                _sidebarVisibilityBeforeFullscreen = SidebarPanel.Visibility;
                _temporaryAddressBarVisibilityBeforeFullscreen = TemporaryAddressBar.Visibility;

                SidebarPanel.Visibility = Visibility.Collapsed;
                TemporaryAddressBar.Visibility = Visibility.Collapsed;
                WebHost.Background = Brushes.Black;
                _mainFullscreenController.Enter(this);
            }
            else
            {
                SidebarPanel.Visibility = _sidebarVisibilityBeforeFullscreen;
                TemporaryAddressBar.Visibility = _temporaryAddressBarVisibilityBeforeFullscreen;
                WebHost.Background = _webHostBackgroundBeforeFullscreen ?? Brushes.White;
                _mainFullscreenController.Exit(this);
            }

            UpdateExtendedFrame();
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
            _temporaryWebView.Source = uri;
            _isTemporaryPageVisible = true;
        }

        private async Task CloseTemporaryPageAsync()
        {
            TemporaryAddressBar.Visibility = Visibility.Collapsed;
            _isTemporaryPageVisible = false;
            _pendingSite = null;

            if (_temporaryWebView is not null)
            {
                ExitWebContentFullscreenIfOwnedBy(_temporaryWebView);
                _temporaryWebView.PreviewKeyDown -= TemporaryWebView_PreviewKeyDown;
                WebHost.Children.Remove(_temporaryWebView);
                _temporaryWebView.Dispose();
                _temporaryWebView = null;
                _temporaryEnvironment = null;
            }

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
            _temporaryEnvironment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: SiteStore.TemporaryProfileFolder);

            await webView.EnsureCoreWebView2Async(_temporaryEnvironment);

            webView.PreviewKeyDown += TemporaryWebView_PreviewKeyDown;

            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                WebView_ContainsFullScreenElementChanged(webView);

            webView.CoreWebView2.NewWindowRequested += (s, args) =>
                OpenWebViewPopupWindow(args, _temporaryEnvironment);

            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                if (!args.IsSuccess && _pendingSite is null)
                {
                    ShowError($"Failed to load temporary page.\n{args.WebErrorStatus}");
                }
            };
        }

        private void TemporaryWebView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            if (e.Key == Key.L)
            {
                if (_isWebContentFullscreen)
                {
                    return;
                }

                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(ShowTemporaryAddressBar));
                return;
            }

            if (e.Key == Key.W && CanCloseTemporaryPage())
            {
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(() => _ = CloseTemporaryPageAsync()));
            }
        }

        private static bool TryNormalizeWebAddress(string input, out Uri uri)
        {
            uri = null!;
            string address = input.Trim();

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            if (!address.Contains("://", StringComparison.Ordinal))
            {
                bool localAddress = address.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                    address.StartsWith("127.", StringComparison.OrdinalIgnoreCase);
                address = $"{(localAddress ? "http" : "https")}://{address}";
            }

            return Uri.TryCreate(address, UriKind.Absolute, out uri!) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Initializes a WebView2 already attached to the visual tree with an isolated
        /// user-data profile, wires up navigation-failure reporting, and supports
        /// SSO/login popups, then navigates to the site's URL.
        /// </summary>
        private async Task InitializeWebViewAsync(WebView2 webView, SiteConfig site)
        {
            string profileFolder = SiteStore.GetProfileFolder(site);
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: profileFolder);

            await webView.EnsureCoreWebView2Async(environment);

            webView.PreviewKeyDown += HostedWebView_PreviewKeyDown;

            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                WebView_ContainsFullScreenElementChanged(webView);

            // Some pages open required work in a separate browser window: SSO/login
            // flows are the common case, but target="_blank" links can use this too.
            webView.CoreWebView2.NewWindowRequested += (s, args) =>
                OpenWebViewPopupWindow(args, environment);

            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                if (!args.IsSuccess && ReferenceEquals(_pendingSite, site))
                {
                    ShowError($"Failed to load \"{site.Name}\".\n{args.WebErrorStatus}");
                }
            };

            webView.Source = new Uri(site.Url);
        }

        private void HostedWebView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control || e.Key != Key.L)
            {
                return;
            }

            if (_isWebContentFullscreen)
            {
                return;
            }

            e.Handled = true;
            Dispatcher.BeginInvoke(new Action(ShowTemporaryAddressBar));
        }

        /// <summary>
        /// Opens popup windows requested by a hosted page in a separate WebView2
        /// window sharing the same profile, so sign-in flows and new-window links work.
        /// </summary>
        private void OpenWebViewPopupWindow(
            CoreWebView2NewWindowRequestedEventArgs args,
            CoreWebView2Environment environment)
        {
            var deferral = args.GetDeferral();

            var popupWindow = new Window
            {
                Title = "Nexus",
                Width = PopupWidth,
                Height = PopupHeight,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var popupWebView = new WebView2();
            var popupFullscreenController = new FullscreenWindowController();
            popupWindow.Content = popupWebView;

            _popupWindows.Add(popupWindow);
            popupWindow.Closing += (_, _) => popupFullscreenController.Exit(popupWindow);
            popupWindow.Closed += (_, _) =>
            {
                _popupWindows.Remove(popupWindow);
                popupWebView.Dispose();
            };

            // Show the window first so the WebView2 control has a parent HWND before
            // EnsureCoreWebView2Async is awaited - otherwise initialization hangs.
            popupWindow.Show();

            _ = InitializePopupWebViewAsync(popupWebView, popupWindow, args, environment, deferral, popupFullscreenController);
        }

        private static async Task InitializePopupWebViewAsync(
            WebView2 popupWebView,
            Window popupWindow,
            CoreWebView2NewWindowRequestedEventArgs args,
            CoreWebView2Environment environment,
            CoreWebView2Deferral deferral,
            FullscreenWindowController popupFullscreenController)
        {
            try
            {
                await popupWebView.EnsureCoreWebView2Async(environment);
                popupWebView.CoreWebView2.WindowCloseRequested += (_, _) => popupWindow.Close();
                var popupFullscreen = false;

                popupWebView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                {
                    bool fullscreen = popupWebView.CoreWebView2.ContainsFullScreenElement;
                    if (fullscreen && popupWindow.Visibility != Visibility.Visible)
                    {
                        return;
                    }

                    if (popupFullscreen == fullscreen)
                    {
                        return;
                    }

                    popupFullscreen = fullscreen;

                    if (fullscreen)
                    {
                        popupFullscreenController.Enter(popupWindow, Brushes.Black);
                    }
                    else
                    {
                        popupFullscreenController.Exit(popupWindow);
                    }
                };

                args.NewWindow = popupWebView.CoreWebView2;
                args.Handled = true;
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

        /// <summary>
        /// Disposes all cached WebView2 instances on shutdown so their browser
        /// processes and profile folder locks are released immediately.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _source?.RemoveHook(WndProc);
            _source = null;

            ExitAnyWebContentFullscreen();

            foreach (var popupWindow in _popupWindows.ToList())
            {
                popupWindow.Close();
            }

            _popupWindows.Clear();

            foreach (var webView in _webViews.Values)
            {
                webView.Dispose();
            }

            _webViews.Clear();

            _temporaryWebView?.Dispose();
            _temporaryWebView = null;
            _temporaryEnvironment = null;
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
