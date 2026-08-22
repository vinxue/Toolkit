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

        private Task<CoreWebView2Environment>? _environmentTask;

        public ProfileManager(string userDataFolder) => _userDataFolder = userDataFolder;

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
        }

        private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            // Renderer and utility failures are page-local; WebView2 recovers itself.
            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                BrowserProcessFailed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Reset() => _environmentTask = null;

        public static Task ClearBrowsingDataAsync(WebView2 webView, CoreWebView2BrowsingDataKinds kinds) =>
            webView.CoreWebView2.Profile.ClearBrowsingDataAsync(kinds);
    }
}
