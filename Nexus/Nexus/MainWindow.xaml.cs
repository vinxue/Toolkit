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
        private const int WmDpiChanged = 0x02E0;

        private readonly ObservableCollection<SiteConfig> _sites = new();
        private readonly Dictionary<SiteConfig, WebView2> _webViews = new();

        private IntPtr _hwnd;
        private HwndSource? _source;

        private bool _isSidebarExpanded;
        private Point _dragStartPoint;
        private SiteConfig? _dragCandidate;

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
            double sidebarWidth = SidebarPanel.Width;
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
            var dialog = new AddSiteWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is not null)
            {
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
            ErrorHint.Text = message;
            ErrorHint.Visibility = Visibility.Visible;
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

            // Many corporate SSO flows open a separate popup window to sign in.
            // Without handling this, the popup is dropped and the main page stays blank.
            webView.CoreWebView2.NewWindowRequested += (s, args) =>
                OnNewWindowRequested(s, args, environment);

            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                if (!args.IsSuccess && ReferenceEquals(_pendingSite, site))
                {
                    ShowError($"Failed to load \"{site.Name}\".\n{args.WebErrorStatus}");
                }
            };

            webView.Source = new Uri(site.Url);
        }

        /// <summary>
        /// Opens SSO/login popup windows requested by a hosted site in a separate WPF
        /// window sharing the same profile, so the sign-in flow can complete normally.
        /// </summary>
        private void OnNewWindowRequested(
            object? sender,
            CoreWebView2NewWindowRequestedEventArgs args,
            CoreWebView2Environment environment)
        {
            var deferral = args.GetDeferral();

            var popupWindow = new Window
            {
                Title = "Sign in",
                Width = 480,
                Height = 640,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var popupWebView = new WebView2();
            popupWindow.Content = popupWebView;

            popupWindow.Closed += (_, _) => popupWebView.Dispose();

            // Show the window first so the WebView2 control has a parent HWND before
            // EnsureCoreWebView2Async is awaited - otherwise initialization hangs.
            popupWindow.Show();

            _ = InitializePopupAsync(popupWebView, popupWindow, args, environment, deferral);
        }

        private static async Task InitializePopupAsync(
            WebView2 popupWebView,
            Window popupWindow,
            CoreWebView2NewWindowRequestedEventArgs args,
            CoreWebView2Environment environment,
            CoreWebView2Deferral deferral)
        {
            try
            {
                await popupWebView.EnsureCoreWebView2Async(environment);
                popupWebView.CoreWebView2.WindowCloseRequested += (_, _) => popupWindow.Close();
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

            foreach (var webView in _webViews.Values)
            {
                webView.Dispose();
            }

            _webViews.Clear();
        }
    }
}
