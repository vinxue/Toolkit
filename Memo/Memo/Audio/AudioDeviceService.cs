using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Memo.Audio;

public sealed record AudioDevice(string Id, string Name);

/// <summary>Enumerates audio endpoints and reports hot-plug changes on the thread that created it.</summary>
internal sealed class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDeviceNotificationClient _notifications;

    public AudioDeviceService()
    {
        _notifications = _enumerator.CreateNotificationClient(true);
        _notifications.DeviceAdded += OnChanged;
        _notifications.DeviceRemoved += OnChanged;
        _notifications.DeviceStateChanged += OnChanged;
        _notifications.DefaultDeviceChanged += OnChanged;
    }

    /// <summary>A device was added, removed, enabled/disabled, or a default device changed.</summary>
    public event EventHandler? DevicesChanged;

    public IReadOnlyList<AudioDevice> GetDevices(DataFlow flow)
    {
        var result = new List<AudioDevice>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
                result.Add(new AudioDevice(device.ID, device.FriendlyName));
        }
        return result;
    }

    /// <summary>Teams and most meeting apps use the communications endpoint, which can differ from the console default.</summary>
    public string? GetDefaultDeviceId(DataFlow flow)
    {
        if (!_enumerator.HasDefaultAudioEndpoint(flow, Role.Communications))
            return null;
        using var device = _enumerator.GetDefaultAudioEndpoint(flow, Role.Communications);
        return device.ID;
    }

    public MMDevice GetDevice(string id)
    {
        try
        {
            return _enumerator.GetDevice(id);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException("The audio device is unavailable. It may have been removed.", ex);
        }
    }

    /// <summary>Process ids that currently own an audio session on any active playback device.</summary>
    public HashSet<uint> GetAudioSessionProcessIds()
    {
        var ids = new HashSet<uint>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                try
                {
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];
                        if (!session.IsSystemSoundsSession)
                            ids.Add(session.GetProcessID);
                    }
                }
                catch (COMException)
                {
                    // Device went away mid-enumeration.
                }
            }
        }
        return ids;
    }

    public void Dispose()
    {
        _notifications.Dispose();
        _enumerator.Dispose();
    }

    private void OnChanged(object? sender, EventArgs e) => DevicesChanged?.Invoke(this, EventArgs.Empty);
}
