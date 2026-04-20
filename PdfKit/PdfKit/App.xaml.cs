using System;
using System.Globalization;
using System.Windows;

namespace PdfKit
{
    public partial class App : Application
    {
        // Supported language codes mapped to their resource file names
        public static readonly string[] SupportedLangs = { "en", "zh-CN", "zh-TW" };

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string lang = DetectLanguage();
            ApplyLanguage(lang);
        }

        private static string DetectLanguage()
        {
            string culture = CultureInfo.CurrentUICulture.Name; // e.g. "zh-CN", "zh-TW", "zh-HK"
            if (culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                culture.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase))
                return "zh-TW";
            if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";
            return "en";
        }

        /// <summary>
        /// Swap the language ResourceDictionary at runtime.
        /// Call this from MainWindow to switch language without restart.
        /// </summary>
        public static void ApplyLanguage(string lang)
        {
            string uri = $"Resources/Strings.{lang}.xaml";
            var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

            var merged = Current.Resources.MergedDictionaries;
            // Remove any previously loaded Strings.*.xaml
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source?.OriginalString ?? string.Empty;
                if (src.Contains("Strings."))
                    merged.RemoveAt(i);
            }
            merged.Add(dict);
        }

        /// <summary>Helper: retrieve a localised string by key.</summary>
        public static string S(string key)
        {
            return Current.TryFindResource(key) as string ?? key;
        }
    }
}
