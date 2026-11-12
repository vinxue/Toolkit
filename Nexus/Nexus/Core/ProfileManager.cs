using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Nexus.Core
{
    /// <summary>
    /// Owns the single WebView2 environment. Every profile lives inside one user
    /// data folder, so all sites share one browser process group instead of
    /// starting a separate browser, GPU and network process per site.
    /// </summary>
    public sealed class ProfileManager
    {
        private readonly string _userDataFolder;

        // Color scheme is a per-profile setting, so one live object per profile is enough to update it.
        private readonly Dictionary<ProfileContext, CoreWebView2Profile> _profiles = new();

        private Task<CoreWebView2Environment>? _environmentTask;

        public ProfileManager(string userDataFolder) => _userDataFolder = userDataFolder;

        public CoreWebView2PreferredColorScheme ColorScheme { get; private set; } = CoreWebView2PreferredColorScheme.Auto;

        /// <summary>
        /// Raised when the shared browser process dies, which invalidates every
        /// WebView2 in the app rather than just the one that reported it.
        /// </summary>
        public event EventHandler? BrowserProcessFailed;

        /// <summary>
        /// Caches the task rather than the result: several sites can start
        /// initializing before the first CreateAsync completes, which would
        /// otherwise open two environments over the same user data folder.
        /// </summary>
        public Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environmentTask is null || _environmentTask.IsFaulted || _environmentTask.IsCanceled)
            {
                _environmentTask = CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: _userDataFolder);
            }

            return _environmentTask;
        }

        public async Task InitializeAsync(WebView2 webView, ProfileContext profile)
        {
            var environment = await GetEnvironmentAsync();
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.ProfileName = profile.Name;
            options.IsInPrivateModeEnabled = profile.IsInPrivate;

            await webView.EnsureCoreWebView2Async(environment, options);

            webView.CoreWebView2.ProcessFailed += OnProcessFailed;

            var coreProfile = webView.CoreWebView2.Profile;
            coreProfile.PreferredColorScheme = ColorScheme;
            _profiles[profile] = coreProfile;
        }

        /// <summary>
        /// Tells pages which light/dark scheme to use (prefers-color-scheme) in every profile opened so far.
        /// </summary>
        public void SetColorScheme(CoreWebView2PreferredColorScheme scheme)
        {
            ColorScheme = scheme;

            foreach (var coreProfile in _profiles.Values)
            {
                try
                {
                    coreProfile.PreferredColorScheme = scheme;
                }
                catch (Exception ex) when (ex is InvalidOperationException or COMException)
                {
                    // Every WebView2 on this profile is gone; the next one picks up ColorScheme on init.
                }
            }
        }

        private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            // Renderer and utility failures are page-local; WebView2 recovers itself.
            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                BrowserProcessFailed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Reset()
        {
            _environmentTask = null;
            _profiles.Clear();
        }

        public static Task ClearBrowsingDataAsync(WebView2 webView, CoreWebView2BrowsingDataKinds kinds) =>
            webView.CoreWebView2.Profile.ClearBrowsingDataAsync(kinds);
    }
}
