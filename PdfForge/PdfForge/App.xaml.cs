using System.Globalization;
using System.Windows;

namespace PdfForge
{
    public partial class App : Application
    {
        public static readonly string[] SupportedLangs = { "en", "zh-CN", "zh-TW" };

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string lang = DetectLanguage();
            ApplyLanguage(lang);
        }

        private static string DetectLanguage()
        {
            string culture = CultureInfo.CurrentUICulture.Name;
            if (culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                culture.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase))
                return "zh-TW";
            if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";
            return "en";
        }

        public static string CurrentLang { get; private set; } = "en";

        public static void ApplyLanguage(string lang)
        {
            CurrentLang = lang;
            string uri = $"Resources/Strings.{lang}.xaml";
            var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

            var merged = Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source?.OriginalString ?? string.Empty;
                if (src.Contains("Strings."))
                    merged.RemoveAt(i);
            }
            merged.Add(dict);
        }

        public static string S(string key)
        {
            return Current.TryFindResource(key) as string ?? key;
        }
    }
}
