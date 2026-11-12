using System.Windows;

namespace Nexus.Core
{
    internal sealed class PopupWindowManager
    {
        private readonly Dictionary<Window, string> _windows = new();

        public void Track(Window window, string profileName)
        {
            _windows[window] = profileName;
            window.Closed += (_, _) => _windows.Remove(window);
        }

        /// <summary>
        /// A profile cannot finish deleting while windows still use it.
        /// </summary>
        public void CloseForProfile(string profileName)
        {
            var matches = _windows
                .Where(pair => string.Equals(pair.Value, profileName, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList();

            foreach (var window in matches)
            {
                window.Close();
            }
        }

        public void CloseAll()
        {
            foreach (var window in _windows.Keys.ToList())
            {
                window.Close();
            }

            _windows.Clear();
        }
    }
}
