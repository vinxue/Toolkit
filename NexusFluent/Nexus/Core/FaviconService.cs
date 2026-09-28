using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Nexus.Models;

namespace Nexus.Core
{
    /// <summary>
    /// Keeps each site's favicon on disk so the sidebar shows real icons right at
    /// startup, before any site has been opened in this session.
    /// </summary>
    public static class FaviconService
    {
        private readonly record struct CacheLocation(string Folder, string File);

        public static void LoadCached(SiteConfig site, string cacheFolder)
        {
            if (ResolveCachePath(site, cacheFolder) is not { } location || !File.Exists(location.File))
            {
                return;
            }

            try
            {
                site.Favicon = CreateImage(File.ReadAllBytes(location.File));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        public static async Task UpdateAsync(CoreWebView2 core, SiteConfig site, string cacheFolder)
        {
            byte[] bytes;

            try
            {
                using var favicon = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
                if (favicon is null)
                {
                    return;
                }

                using var buffer = new MemoryStream();
                await favicon.CopyToAsync(buffer);
                bytes = buffer.ToArray();
            }
            catch (Exception)
            {
                // The page may have no favicon, or navigated away mid-read.
                return;
            }

            if (bytes.Length == 0 || CreateImage(bytes) is not { } image)
            {
                return;
            }

            site.Favicon = image;
            TryCache(site, cacheFolder, bytes);
        }

        public static void DeleteCached(SiteConfig site, string cacheFolder)
        {
            if (ResolveCachePath(site, cacheFolder) is not { } location)
            {
                return;
            }

            try
            {
                File.Delete(location.File);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static void TryCache(SiteConfig site, string cacheFolder, byte[] bytes)
        {
            if (ResolveCachePath(site, cacheFolder) is not { } location)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(location.Folder);
                File.WriteAllBytes(location.File, bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Ids come from a config file that can be hand-edited, so the file name is
        /// restricted to alphanumerics rather than trusted as a path segment.
        /// </summary>
        private static CacheLocation? ResolveCachePath(SiteConfig site, string cacheFolder)
        {
            if (string.IsNullOrEmpty(site.Id) || !site.Id.All(char.IsAsciiLetterOrDigit))
            {
                return null;
            }

            return new CacheLocation(cacheFolder, Path.Combine(cacheFolder, $"{site.Id}.png"));
        }

        private static ImageSource? CreateImage(byte[] bytes)
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }
    }
}
