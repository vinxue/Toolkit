using System.Windows;
using Nexus.Models;
using Nexus.Services;

namespace Nexus
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Surface any unhandled error instead of leaving the window blank/silent.
            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show(
                    $"Nexus hit an unexpected error and may not work correctly until it is restarted.\n\n{args.Exception}",
                    "Unexpected error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            ThemeMode = ToWpfThemeMode(ConfigStore.Config.Theme);

            base.OnStartup(e);
        }

        internal static ThemeMode ToWpfThemeMode(AppTheme theme) => theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }
}
