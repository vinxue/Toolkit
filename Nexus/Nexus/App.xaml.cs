using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

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
                    args.Exception.ToString(),
                    "Unexpected error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }

}
