using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using CalendarApp.Core;

namespace CalendarApp
{
    public partial class App : Application
    {
        private const string InstanceId = "Local\\Calendar.4e7a2c9b";

        private readonly Mutex _instanceMutex;
        private readonly EventWaitHandle _showEvent;
        private RegisteredWaitHandle? _showWaitHandle;

        public App()
        {
            _instanceMutex = new Mutex(true, InstanceId, out bool isFirst);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceId + ".Show");

            if (!isFirst)
            {
                if (!HasTrayArg(Environment.GetCommandLineArgs().Skip(1)))
                {
                    // The launching process owns foreground rights; hand them to the running instance.
                    using var self = Process.GetCurrentProcess();
                    foreach (var other in Process.GetProcessesByName(self.ProcessName))
                    {
                        if (other.Id != self.Id)
                        {
                            AllowSetForegroundWindow(other.Id);
                        }

                        other.Dispose();
                    }

                    _showEvent.Set();
                }

                Environment.Exit(0);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AutoStart.RepairIfMoved();

            var window = new MainWindow();
            if (HasTrayArg(e.Args))
            {
                window.StartInTray();
            }
            else
            {
                window.Show();
            }

            // Registered after MainWindow exists so an early second launch stays queued in _showEvent.
            _showWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _showEvent,
                (_, _) => Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow main)
                    {
                        main.ShowFromTray();
                    }
                }),
                null,
                Timeout.Infinite,
                false);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _showWaitHandle?.Unregister(null);
            base.OnExit(e);
        }

        private static bool HasTrayArg(IEnumerable<string> args) =>
            args.Contains(AutoStart.TrayArgument, StringComparer.OrdinalIgnoreCase);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);
    }
}
