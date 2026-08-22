using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Nexus.Core;
using Nexus.Services;

namespace Nexus
{
    public partial class PopupWebViewWindow : Window
    {
        private readonly ProfileManager _profileManager;
        private readonly ProfileContext _profile;
        private readonly Action<CoreWebView2NewWindowRequestedEventArgs, ProfileContext> _openPopupWindow;
        private readonly Func<ProfileContext, Task> _openNewWindowAsync;
        private readonly FullscreenWindowController _appFullscreenController = new();
        private readonly FullscreenWindowController _webFullscreenController = new();

        private bool _isAppFullscreen;
        private bool _isWebContentFullscreen;
        private bool _isInitialized;
        private ChromeState? _chromeBeforeFullscreen;

        public PopupWebViewWindow(
            ProfileManager profileManager,
            ProfileContext profile,
            Action<CoreWebView2NewWindowRequestedEventArgs, ProfileContext> openPopupWindow,
            Func<ProfileContext, Task> openNewWindowAsync)
        {
            _profileManager = profileManager;
            _profile = profile;
            _openPopupWindow = openPopupWindow;
            _openNewWindowAsync = openNewWindowAsync;
            InitializeComponent();

            // Link popups have no address bar, so the title is the only place a
            // private window can be told apart.
            if (profile.IsInPrivate)
            {
                Title = "Private - Nexus";
            }
        }

        public async Task InitializeAsync(CoreWebView2NewWindowRequestedEventArgs args)
        {
            await InitializeCoreAsync();

            args.NewWindow = PopupWebView.CoreWebView2;
            args.Handled = true;
        }

        public async Task InitializeStandaloneAsync()
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

            await _profileManager.InitializeAsync(PopupWebView, _profile);

            PopupWebView.CoreWebView2.WindowCloseRequested += (_, _) => Close();
            PopupWebView.CoreWebView2.NewWindowRequested += (_, popupArgs) =>
                _openPopupWindow(popupArgs, _profile);
            PopupWebView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
                SetWebContentFullscreen(PopupWebView.CoreWebView2.ContainsFullScreenElement);

            _isInitialized = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) =>
            HandleShortcut(e, defer: false);

        /// <summary>
        /// Shortcuts arriving from the WebView2 run after its native key handling
        /// returns, otherwise focus changes made here are swallowed.
        /// </summary>
        private void PopupWebView_PreviewKeyDown(object sender, KeyEventArgs e) =>
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
                Key.N when !_isWebContentFullscreen => () => _ = _openNewWindowAsync(newWindowProfile),
                Key.L => ShowAddressBar,
                // An open address bar means the user is typing, so Ctrl+W must not
                // close the window from under them.
                Key.W when PopupAddressBar.Visibility != Visibility.Visible => Close,
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
                _chromeBeforeFullscreen = new ChromeState(PopupRoot.Background, PopupAddressBar.Visibility);
                PopupAddressBar.Visibility = Visibility.Collapsed;
                PopupRoot.Background = Brushes.Black;
                controller.Enter(this, Brushes.Black);
            }
            else
            {
                PopupAddressBar.Visibility = _chromeBeforeFullscreen?.AddressBar ?? Visibility.Collapsed;
                PopupRoot.Background = _chromeBeforeFullscreen?.RootBackground ?? Brushes.White;
                _chromeBeforeFullscreen = null;
                controller.Exit(this);
            }
        }

        private readonly record struct ChromeState(Brush? RootBackground, Visibility AddressBar);

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

            // Navigate rather than assigning Source, which no-ops when the address is
            // unchanged and would make re-entering the current URL do nothing.
            if (PopupWebView.CoreWebView2 is { } core)
            {
                core.Navigate(uri.AbsoluteUri);
            }
            else
            {
                PopupWebView.Source = uri;
            }

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
            SetChromeHidden(fullscreen, _webFullscreenController);
        }

        protected override void OnClosed(EventArgs e)
        {
            _appFullscreenController.Exit(this);
            _webFullscreenController.Exit(this);

            try
            {
                PopupWebView.Dispose();
            }
            catch (Exception)
            {
                // A browser process failure already tore the CoreWebView2 down.
            }

            base.OnClosed(e);
        }

    }
}
