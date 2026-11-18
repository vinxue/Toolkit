using System.IO;
using System.Security;
using Microsoft.Win32;

namespace CalendarApp.Core
{
    // Per-user launch at sign-in through the HKCU Run key; needs no elevation.
    public static class AutoStart
    {
        public const string TrayArgument = "--tray";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ValueName = "CalendarApp";

        private static string? GetCommand() =>
            Environment.ProcessPath is { Length: > 0 } path ? $"\"{path}\" {TrayArgument}" : null;

        public static bool IsEnabled
        {
            get
            {
                if (GetCommand() is not string command)
                {
                    return false;
                }

                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string existing
                        && string.Equals(existing, command, StringComparison.OrdinalIgnoreCase)
                        && !IsDisabledByApproval();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
                {
                    return false;
                }
            }
        }

        // Returns the executable path when another existing copy owns the active Run entry.
        public static string? GetOtherRegisteredPath()
        {
            if (Environment.ProcessPath is not { Length: > 0 } currentPath)
            {
                return null;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                if (key?.GetValue(ValueName) is not string existing || IsDisabledByApproval())
                {
                    return null;
                }

                string? existingPath = GetExecutablePath(existing);
                return existingPath is not null
                    && !string.Equals(existingPath, currentPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(existingPath)
                    ? existingPath
                    : null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                return null;
            }
        }

        // Updates the Run entry when the app folder was moved or the command format drifted.
        public static void RepairIfMoved()
        {
            if (Environment.ProcessPath is not { Length: > 0 } currentPath ||
                GetCommand() is not string command)
            {
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                if (key?.GetValue(ValueName) is not string existing ||
                    string.Equals(existing, command, StringComparison.OrdinalIgnoreCase) ||
                    IsDisabledByApproval())
                {
                    return;
                }

                string? existingPath = GetExecutablePath(existing);
                if (existingPath is null ||
                    string.Equals(existingPath, currentPath, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(existingPath))
                {
                    using var writable = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                    writable?.SetValue(ValueName, command);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
            }
        }

        public static bool TrySetEnabled(bool enabled)
        {
            string? command = GetCommand();
            if (enabled && command is null)
            {
                return false;
            }

            try
            {
                if (enabled)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                    if (key is null)
                    {
                        return false;
                    }

                    key.SetValue(ValueName, command!);
                    TryClearApprovalOverride();
                    return !IsDisabledByApproval();
                }
                else
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                    key?.DeleteValue(ValueName, throwOnMissingValue: false);
                    TryClearApprovalOverride();
                    return true;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                return false;
            }
        }

        // Task Manager / Settings disables an entry here without removing it from Run.
        private static bool IsDisabledByApproval()
        {
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return approved?.GetValue(ValueName) is byte[] { Length: > 0 } status && (status[0] & 1) != 0;
        }

        private static void TryClearApprovalOverride()
        {
            try
            {
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
                approved?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
            }
        }

        private static string? GetExecutablePath(string commandLine)
        {
            ReadOnlySpan<char> span = commandLine.AsSpan().TrimStart();
            if (span.IsEmpty)
            {
                return null;
            }

            if (span[0] == '"')
            {
                int endQuote = span[1..].IndexOf('"');
                return endQuote > 0 ? span.Slice(1, endQuote).ToString() : null;
            }

            int separator = span.IndexOfAny(' ', '\t');
            return (separator < 0 ? span : span[..separator]).ToString();
        }
    }
}
