using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace CalendarApp
{
    public partial class App : Application
    {
        private const string InstanceId = "Local\\Calendar.4e7a2c9b";

        private readonly Mutex _instanceMutex;
        private readonly EventWaitHandle _showEvent;

        public App()
        {
            _instanceMutex = new Mutex(true, InstanceId, out bool isFirst);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceId + ".Show");

            if (!isFirst)
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
                Environment.Exit(0);
            }

            ThreadPool.RegisterWaitForSingleObject(
                _showEvent,
                (_, _) => Dispatcher.BeginInvoke(() => Windows.OfType<MainWindow>().FirstOrDefault()?.ShowFromTray()),
                null,
                Timeout.Infinite,
                false);
        }

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);
    }
}
