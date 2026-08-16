using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace Nexus
{
    public partial class PopupWebViewWindow : Window
    {
        private readonly CoreWebView2Environment _environment;
        private readonly Action<CoreWebView2NewWindowRequestedEventArgs, CoreWebView2Environment> _openPopupWindow;
        private readonly Func<Task> _openTemporaryPopupWindowAsync;
        private readonly FullscreenWindowController _appFullscreenController = new();
        private readonly FullscreenWindowController _webFullscreenController = new();

        private bool _isAppFullscreen;
        private bool _isWebContentFullscreen;
        private bool _isInitialized;
        private Brush? _popupRootBackgroundBeforeAppFullscreen;
        private Brush? _popupRootBackgroundBeforeWebFullscreen;
        private Visibility _addressBarVisibilityBeforeAppFullscreen;
        private Visibility _addressBarVisibilityBeforeWebFullscreen;

        public PopupWebViewWindow(
            CoreWebView2Environment environment,
            Action<CoreWebView2NewWindowRequestedEventArgs, CoreWebView2Environment> openPopupWindow,
            Func<Task> openTemporaryPopupWindowAsync)
        {
            _environment = environment;
            _openPopupWindow = openPopupWindow;
            _openTemporaryPopupWindowAsync = openTemporaryPopupWindowAsync;
            InitializeComponent();
        }

        public async Task InitializeAsync(CoreWebView2NewWindowRequestedEventArgs args)
        {
            await InitializeCoreAsync();

            args.NewWindow = PopupWebView.CoreWebView2;
            args.Handled = true;
        }

        public async Task InitializeTemporaryAsync()
        {
            await InitializeCoreAsync();
            ShowAddressBar();
        }

        private async Task InitializeCoreAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await PopupWebView.EnsureCoreWebView2Async(_environment);

            PopupWebView.CoreWebView2.WindowCloseRequested += (_, _) => Close();
            PopupWebView.CoreWebView2.NewWindowRequested += (_, popupArgs) =>
                _openPopupWindow(popupArgs, _environment);
            PopupWebView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                SetWebContentFullscreen(PopupWebView.CoreWebView2.ContainsFullScreenElement);

            _isInitialized = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
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

            if (e.Key == Key.N)
            {
                if (_isWebContentFullscreen)
                {
                    return;
                }

                e.Handled = true;
                _ = _openTemporaryPopupWindowAsync();
                return;
            }

            if (e.Key == Key.L)
            {
                ShowAddressBar();
                e.Handled = true;
            }
        }

        private void PopupWebView_PreviewKeyDown(object sender, KeyEventArgs e)
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

            if (e.Key == Key.N)
            {
                if (_isWebContentFullscreen)
                {
                    return;
                }

                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(() => _ = _openTemporaryPopupWindowAsync()));
                return;
            }

            if (e.Key == Key.L)
            {
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(ShowAddressBar));
            }
        }

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

            if (fullscreen)
            {
                _popupRootBackgroundBeforeAppFullscreen = PopupRoot.Background;
                _addressBarVisibilityBeforeAppFullscreen = PopupAddressBar.Visibility;
                PopupAddressBar.Visibility = Visibility.Collapsed;
                PopupRoot.Background = Brushes.Black;
                _appFullscreenController.Enter(this, Brushes.Black);
            }
            else
            {
                PopupAddressBar.Visibility = _addressBarVisibilityBeforeAppFullscreen;
                PopupRoot.Background = _popupRootBackgroundBeforeAppFullscreen ?? Brushes.White;
                _appFullscreenController.Exit(this);
            }
        }

        private void ShowAddressBar()
        {
            if (_isWebContentFullscreen)
            {
                return;
            }

            PopupAddressBar.Visibility = Visibility.Visible;
            PopupAddressBox.Text = PopupWebView.Source?.AbsoluteUri ?? string.Empty;
            UpdateAddressPlaceholderVisibility();
            PopupAddressBox.Focus();
            PopupAddressBox.SelectAll();
        }

        private void PopupAddressBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
            UpdateAddressPlaceholderVisibility();

        private void UpdateAddressPlaceholderVisibility()
        {
            PopupAddressPlaceholderText.Visibility = string.IsNullOrEmpty(PopupAddressBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PopupAddressBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                PopupAddressBar.Visibility = Visibility.Collapsed;
                PopupWebView.Focus();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;

            if (!WebAddressHelper.TryNormalize(PopupAddressBox.Text, out var uri))
            {
                return;
            }

            PopupAddressBar.Visibility = Visibility.Collapsed;
            PopupWebView.Source = uri;
            PopupWebView.Focus();
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

            if (fullscreen)
            {
                _popupRootBackgroundBeforeWebFullscreen = PopupRoot.Background;
                _addressBarVisibilityBeforeWebFullscreen = PopupAddressBar.Visibility;
                PopupAddressBar.Visibility = Visibility.Collapsed;
                PopupRoot.Background = Brushes.Black;
                _webFullscreenController.Enter(this, Brushes.Black);
            }
            else
            {
                PopupAddressBar.Visibility = _addressBarVisibilityBeforeWebFullscreen;
                PopupRoot.Background = _popupRootBackgroundBeforeWebFullscreen ?? Brushes.White;
                _webFullscreenController.Exit(this);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _appFullscreenController.Exit(this);
            _webFullscreenController.Exit(this);
            PopupWebView.Dispose();
            base.OnClosed(e);
        }

    }
}
