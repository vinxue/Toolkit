using System.IO;
using System.Text;
using System.Text.Json;
using Nexus.Models;

namespace Nexus.Services
{
    /// <summary>
    /// Loads and saves the list of hosted websites to a JSON file kept next to the app
    /// (falling back to %AppData%\Nexus if that location isn't writable, e.g. when
    /// installed under Program Files). Uses only built-in System.Text.Json.
    /// </summary>
    public static class SiteStore
    {
        private static readonly string AppFolder = ResolveAppFolder();

        private static readonly string ConfigPath = Path.Combine(AppFolder, "sites.json");

        /// <summary>
        /// Root folder that holds one isolated WebView2 profile per site.
        /// </summary>
        public static string ProfilesRoot => Path.Combine(AppFolder, "profiles");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Picks the folder used to store app data: a "data" folder next to the
        /// executable when that location is writable (portable use), otherwise
        /// %AppData%\Nexus (e.g. when installed under Program Files).
        /// </summary>
        private static string ResolveAppFolder()
        {
            string nextToExe = Path.Combine(AppContext.BaseDirectory, "data");
            if (IsWritable(nextToExe))
            {
                return nextToExe;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexus");
        }

        private static bool IsWritable(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string probeFile = Path.Combine(folder, ".write-check");
                File.WriteAllText(probeFile, string.Empty);
                File.Delete(probeFile);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the site list, seeding the default sites on first run.
        /// </summary>
        public static List<SiteConfig> Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var sites = JsonSerializer.Deserialize<List<SiteConfig>>(json, JsonOptions);
                    if (sites is { Count: > 0 })
                    {
                        return sites;
                    }
                }
            }
            catch
            {
                // Fall through to defaults on any read/parse error.
            }

            var defaults = CreateDefaults();
            Save(defaults);
            return defaults;
        }

        /// <summary>
        /// Persists the site list to disk.
        /// </summary>
        public static void Save(IEnumerable<SiteConfig> sites)
        {
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(sites, JsonOptions);
            File.WriteAllText(ConfigPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Returns an isolated WebView2 user-data folder path for the given site.
        /// </summary>
        public static string GetProfileFolder(SiteConfig site)
        {
            string safeName = Sanitize(site.Name);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "site";
            }

            return Path.Combine(ProfilesRoot, safeName);
        }

        /// <summary>
        /// Deletes the isolated profile folder (login/cookies/cache) for a removed site.
        /// Any failure (e.g. files still in use) is ignored - this is best-effort cleanup.
        /// </summary>
        public static void DeleteProfileFolder(SiteConfig site)
        {
            try
            {
                string folder = GetProfileFolder(site);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private static string Sanitize(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }

        private static List<SiteConfig> CreateDefaults() => new()
        {
            new SiteConfig { Name = "Google", Url = "https://www.google.com/" }
        };
    }
}
