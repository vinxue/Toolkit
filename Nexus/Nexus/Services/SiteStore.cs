using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Core;
using Nexus.Models;

namespace Nexus.Services
{
    /// <summary>
    /// Loads and saves the list of hosted websites under a writable app data folder,
    /// migrating older app-adjacent sites.json files when present.
    /// </summary>
    public static class SiteStore
    {
        private const int ProfileIdSuffixLength = 7;
        private const int MaxProfileNameLength = 64;
        private const string SiteProfileNamePrefix = "site-";

        public const string SharedProfileName = "shared";
        public const string PrivateProfileName = "private";

        private static readonly string AppFolder = ResolveAppFolder();

        private static readonly string ConfigPath = Path.Combine(AppFolder, "sites.json");

        /// <summary>
        /// Where sites.json and the WebView2 profiles live. Resolved at startup, so
        /// it is surfaced in the UI rather than left for the user to guess.
        /// </summary>
        public static string DataFolder => AppFolder;

        /// <summary>
        /// Cached site icons, kept outside the WebView2 user data folder so clearing
        /// browsing data does not blank the sidebar.
        /// </summary>
        public static string FaviconFolder => Path.Combine(AppFolder, "favicons");

        /// <summary>
        /// The single WebView2 user data folder. Sites are separated by profile name
        /// inside it, which keeps one browser process group for the whole app.
        /// </summary>
        public static string UserDataFolder => Path.Combine(AppFolder, "webview2");

        public static ProfileContext SharedProfile => new(SharedProfileName, IsInPrivate: false);

        public static ProfileContext PrivateProfile => new(PrivateProfileName, IsInPrivate: true);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
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
                        if (EnsureProfileNames(sites))
                        {
                            Save(sites);
                        }

                        return sites;
                    }
                }
            }
            catch
            {
                BackupCorruptConfig();
            }

            var defaults = CreateDefaults();
            EnsureProfileNames(defaults);
            Save(defaults);
            return defaults;
        }

        /// <summary>
        /// Persists the site list to disk. Written to a temporary file first so an
        /// interrupted write cannot leave a truncated config behind.
        /// </summary>
        public static void Save(IEnumerable<SiteConfig> sites)
        {
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(sites, JsonOptions);
            string tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, ConfigPath, overwrite: true);
        }

        /// <summary>
        /// Returns the WebView2 profile the given site must be hosted in.
        /// </summary>
        public static ProfileContext GetProfileContext(SiteConfig site)
        {
            if (!UsesIsolatedProfile(site))
            {
                return SharedProfile;
            }

            EnsureProfileName(site);
            return new ProfileContext(site.ProfileName, IsInPrivate: false);
        }

        public static bool UsesIsolatedProfile(SiteConfig site) =>
            site.ProfileMode == SiteProfileMode.Isolated;

        /// <summary>
        /// True while the site still owns isolated profile data, including after it
        /// has been switched back to the shared profile.
        /// </summary>
        public static bool HasSiteProfile(SiteConfig site) =>
            IsUsableSiteProfileName(site.ProfileName);

        public static bool EnsureProfileConfiguration(SiteConfig site, IEnumerable<SiteConfig>? siblings = null)
        {
            if (!UsesIsolatedProfile(site))
            {
                return false;
            }

            return EnsureProfileName(site, siblings);
        }

        public static bool EnsureProfileName(SiteConfig site, IEnumerable<SiteConfig>? siblings = null)
        {
            if (IsUsableSiteProfileName(site.ProfileName) && !IsProfileNameTaken(site.ProfileName, site, siblings))
            {
                return false;
            }

            string id = SanitizeProfileSegment(site.Id);
            if (id.Length == 0)
            {
                site.Id = Guid.NewGuid().ToString("N");
                id = site.Id;
            }

            string label = SanitizeProfileSegment(GetProfileLabel(site));

            // Two sites may legitimately point at the same URL (one account each), so
            // the id suffix grows until the generated name is unique.
            for (int suffixLength = ProfileIdSuffixLength; suffixLength <= id.Length; suffixLength++)
            {
                string candidate = BuildProfileName(label, id[..suffixLength]);
                if (!IsProfileNameTaken(candidate, site, siblings))
                {
                    site.ProfileName = candidate;
                    return true;
                }
            }

            site.ProfileName = BuildProfileName(label, Guid.NewGuid().ToString("N"));
            return true;
        }

        private static string BuildProfileName(string label, string suffix)
        {
            int roomForLabel = MaxProfileNameLength - SiteProfileNamePrefix.Length - suffix.Length - 1;
            if (label.Length > roomForLabel)
            {
                label = roomForLabel <= 0 ? string.Empty : label[..roomForLabel].Trim('-', '.', '_');
            }

            return label.Length == 0
                ? $"{SiteProfileNamePrefix}{suffix}"
                : $"{SiteProfileNamePrefix}{label}-{suffix}";
        }

        private static bool IsProfileNameTaken(string name, SiteConfig site, IEnumerable<SiteConfig>? siblings) =>
            siblings is not null &&
            siblings.Any(other =>
                !ReferenceEquals(other, site) &&
                string.Equals(other.ProfileName, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// WebView2 only accepts ASCII letters, digits, '.', '-' and '_', at most 64
        /// characters, with no leading or trailing period. Reserved names are rejected
        /// so a hand-edited config cannot point a site at the shared profile.
        /// </summary>
        private static bool IsUsableSiteProfileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxProfileNameLength)
            {
                return false;
            }

            if (name.StartsWith('.') || name.EndsWith('.'))
            {
                return false;
            }

            if (string.Equals(name, SharedProfileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, PrivateProfileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return name.All(IsAllowedProfileNameChar);
        }

        private static bool IsAllowedProfileNameChar(char c) =>
            c is >= 'a' and <= 'z' ||
            c is >= 'A' and <= 'Z' ||
            c is >= '0' and <= '9' ||
            c is '.' or '-' or '_';

        private static string SanitizeProfileSegment(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char c in value.Trim())
            {
                builder.Append(IsAllowedProfileNameChar(c) ? char.ToLowerInvariant(c) : '-');
            }

            return builder.ToString().Trim('-', '.', '_');
        }

        private static bool EnsureProfileNames(IEnumerable<SiteConfig> sites)
        {
            var list = sites as IReadOnlyCollection<SiteConfig> ?? sites.ToList();
            bool changed = false;
            foreach (var site in list)
            {
                changed |= EnsureProfileConfiguration(site, list);
            }

            return changed;
        }

        private static string GetProfileLabel(SiteConfig site)
        {
            if (Uri.TryCreate(site.Url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }

            return site.Name;
        }

        private static void BackupCorruptConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return;
                }

                string backupPath = Path.Combine(
                    AppFolder,
                    $"sites.corrupt.{DateTime.Now:yyyyMMddHHmmss}.json");
                File.Move(ConfigPath, backupPath);
            }
            catch
            {
                // Best-effort only: if backup fails, defaults still let the app start.
            }
        }

        private static List<SiteConfig> CreateDefaults() => new()
        {
            new SiteConfig { Name = "Google", Url = "https://www.google.com/" }
        };
    }
}
