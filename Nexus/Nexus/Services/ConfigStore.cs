using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Models;

namespace Nexus.Services
{
    /// <summary>
    /// Everything Nexus persists apart from WebView2 data.
    /// </summary>
    public sealed class AppConfig
    {
        public AppTheme Theme { get; set; } = AppTheme.System;

        public List<SiteConfig> Sites { get; set; } = new();
    }

    /// <summary>
    /// Owns settings.json. It is read once and every save rewrites the whole document,
    /// so saving sites and saving preferences can never drop each other's changes.
    /// </summary>
    public static class ConfigStore
    {
        private static readonly string ConfigPath = Path.Combine(SiteStore.DataFolder, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private static AppConfig? _config;

        public static AppConfig Config => _config ??= Read();

        /// <summary>
        /// Written to a temporary file first so an interrupted write cannot leave a
        /// truncated config behind.
        /// </summary>
        public static void Save()
        {
            Directory.CreateDirectory(SiteStore.DataFolder);
            string tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Config, JsonOptions), Encoding.UTF8);
            File.Move(tempPath, ConfigPath, overwrite: true);
        }

        private static AppConfig Read()
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            try
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions) ?? new AppConfig();
                if (!Enum.IsDefined(config.Theme))
                {
                    config.Theme = AppTheme.System;
                }

                config.Sites ??= new();
                return config;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                BackupCorruptConfig();
                return new AppConfig();
            }
        }

        private static void BackupCorruptConfig()
        {
            try
            {
                string backupPath = Path.Combine(
                    SiteStore.DataFolder,
                    $"config.corrupt.{DateTime.Now:yyyyMMddHHmmss}.json");
                File.Move(ConfigPath, backupPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort only: if backup fails, defaults still let the app start.
            }
        }
    }
}
