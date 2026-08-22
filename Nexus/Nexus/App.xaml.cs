using System.Windows;

namespace Nexus
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
        }
    }
}
