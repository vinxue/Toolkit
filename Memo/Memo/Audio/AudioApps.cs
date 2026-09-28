using System.ComponentModel;
using System.Diagnostics;

namespace Memo.Audio;

public sealed record AudioApp(string ProcessName, string DisplayName);

/// <summary>Finds running apps that can be targeted by per-process loopback capture.</summary>
internal static class AudioApps
{
    /// <summary>Apps with a visible window or an audio session (meeting apps often sit in the tray).</summary>
    public static IReadOnlyList<AudioApp> GetRunningApps(HashSet<uint> audioSessionProcessIds)
    {
        int self = Environment.ProcessId;
        var apps = new Dictionary<string, AudioApp>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == self || process.Id == 0 || apps.ContainsKey(process.ProcessName))
                        continue;
                    if (process.MainWindowHandle == IntPtr.Zero && !audioSessionProcessIds.Contains((uint)process.Id))
                        continue;

                    apps[process.ProcessName] = new AudioApp(process.ProcessName, GetDisplayName(process));
                }
                catch (InvalidOperationException)
                {
                    // Process exited while enumerating.
                }
            }
        }

        return apps.Values.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Picks the app's main process so that capturing its process tree also covers helper processes
    /// (WebView2, browser audio services) that actually render the audio.
    /// </summary>
    public static uint? FindRootProcessId(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            var withWindow = processes.FirstOrDefault(p => SafeGet(() => p.MainWindowHandle != IntPtr.Zero));
            var target = withWindow ?? processes.OrderBy(p => SafeGet(() => p.StartTime)).FirstOrDefault();
            return target is null ? null : (uint)target.Id;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private static string GetDisplayName(Process process)
    {
        try
        {
            string? description = process.MainModule?.FileVersionInfo.FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
                return $"{description.Trim()} ({process.ProcessName})";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // Elevated or protected process: fall back to the executable name.
        }
        return process.ProcessName;
    }

    private static T? SafeGet<T>(Func<T> getter)
    {
        try
        {
            return getter();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return default;
        }
    }
}
