using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Memo.Audio;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace Memo;

public enum RecorderState
{
    Idle,
    Recording,
    Paused,
    Saving,
}

public enum SystemAudioScope
{
    AllApps,
    SingleApp,
}

public enum StatusKind
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record Option<T>(T Value, string Label);

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int AUDCLNT_E_DEVICE_IN_USE = unchecked((int)0x8889000A);
    private const int DefaultBitrate = 128000;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly AudioDeviceService _devices = new();
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _meterTimer;
    private Recorder? _recorder;
    private CaptureSource? _systemSource;
    private CaptureSource? _microphoneSource;
    private DateTime _nextRetry;
    private bool _isReloadingDevices;
    private bool _refreshPending;
    private bool _isRestarting;

    // Portable app: choices live for the session only so a device that drops and returns is reselected.
    private string? _chosenSystemDeviceId;
    private string? _chosenMicrophoneId;
    private string? _chosenAppName;

    [ObservableProperty]
    private AudioDevice? _selectedSystemAudioDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppScope), nameof(IsDeviceScope))]
    private SystemAudioScope _systemAudioScope;

    [ObservableProperty]
    private AudioApp? _selectedApp;

    [ObservableProperty]
    private AudioDevice? _selectedMicrophone;

    [ObservableProperty]
    private bool _recordSystemAudio;

    [ObservableProperty]
    private bool _recordMicrophone;

    [ObservableProperty]
    private double _systemAudioGain;

    [ObservableProperty]
    private double _microphoneGain;

    [ObservableProperty]
    private bool _isMicrophoneMuted;

    [ObservableProperty]
    private double _systemAudioLevel;

    [ObservableProperty]
    private double _microphoneLevel;

    [ObservableProperty]
    private string _outputFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsM4a))]
    private OutputFormat _format;

    [ObservableProperty]
    private int _bitrate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(IsActive), nameof(IsPaused), nameof(IsSaving), nameof(StateText))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseResumeCommand), nameof(StopCommand), nameof(BrowseFolderCommand), nameof(RefreshAppsCommand))]
    private RecorderState _state;

    [ObservableProperty]
    private TimeSpan _elapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _statusTitle = string.Empty;

    [ObservableProperty]
    private StatusKind _statusKind;

    [ObservableProperty]
    private bool _hasFileActions;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowLastFileCommand), nameof(OpenLastFileCommand))]
    private string? _lastFilePath;

    public bool HasStatus => StatusMessage.Length > 0;

    public MainViewModel()
    {
        _recordSystemAudio = true;
        _systemAudioScope = SystemAudioScope.AllApps;
        _recordMicrophone = true;
        _systemAudioGain = 1.0;
        _microphoneGain = 1.0;
        _outputFolder = GetDefaultOutputFolder();
        _format = OutputFormat.M4a;
        _bitrate = DefaultBitrate;

        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Render, OnMeterTick, _dispatcher);
        _meterTimer.Stop();

        _devices.DevicesChanged += OnDevicesChanged;
        RefreshDevices();
        RefreshApps();
    }

    public ObservableCollection<AudioDevice> SystemAudioDevices { get; } = [];

    public ObservableCollection<AudioDevice> Microphones { get; } = [];

    public ObservableCollection<AudioApp> Apps { get; } = [];

    public IReadOnlyList<Option<SystemAudioScope>> Scopes { get; } =
    [
        new(SystemAudioScope.AllApps, "Playback device"),
        new(SystemAudioScope.SingleApp, "Selected app"),
    ];

    public IReadOnlyList<Option<OutputFormat>> Formats { get; } =
    [
        new(OutputFormat.M4a, "M4A (AAC)"),
        new(OutputFormat.Wav, "WAV (lossless)"),
    ];

    public IReadOnlyList<Option<int>> Bitrates { get; } =
        AudioEncoder.AacBitrates.Select(b => new Option<int>(b, $"{b / 1000} kbps")).ToList();

    public bool IsIdle => State == RecorderState.Idle;

    public bool IsActive => State is RecorderState.Recording or RecorderState.Paused;

    public bool IsPaused => State == RecorderState.Paused;

    public bool IsSaving => State == RecorderState.Saving;

    public bool IsM4a => Format == OutputFormat.M4a;

    public bool IsAppScope => SystemAudioScope == SystemAudioScope.SingleApp;

    public bool IsDeviceScope => !IsAppScope;

    public string StateText => State switch
    {
        RecorderState.Recording => "Recording",
        RecorderState.Paused => "Paused",
        RecorderState.Saving => "Saving…",
        _ => "Ready",
    };

    /// <summary>Stops without encoding so the app can exit immediately; the WAV file is kept.</summary>
    public void StopForExit()
    {
        _meterTimer.Stop();
        ReleaseRecorder(out _);
    }

    public void Dispose()
    {
        StopForExit();
        _devices.DevicesChanged -= OnDevicesChanged;
        _devices.Dispose();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task StartAsync()
    {
        CaptureSource? systemSource = null;
        if (RecordSystemAudio && IsAppScope)
        {
            if (SelectedApp is null)
            {
                SetStatus("Select the app to record, or turn system audio off.", StatusKind.Warning);
                return;
            }
            if (AudioApps.FindRootProcessId(SelectedApp.ProcessName) is not uint processId)
            {
                SetStatus($"{SelectedApp.DisplayName} is not running.", StatusKind.Warning);
                return;
            }
            systemSource = CaptureSource.ForProcess(_devices, processId);
        }
        else if (RecordSystemAudio)
        {
            if (SelectedSystemAudioDevice is null)
            {
                SetStatus("Select a playback device, or turn system audio off.", StatusKind.Warning);
                return;
            }
            systemSource = CaptureSource.ForDevice(_devices, SourceKind.SystemAudio, SelectedSystemAudioDevice.Id);
        }

        if (RecordMicrophone && SelectedMicrophone is null)
        {
            SetStatus("Select a microphone, or turn the microphone off.", StatusKind.Warning);
            return;
        }
        var microphone = RecordMicrophone ? SelectedMicrophone : null;
        if (systemSource is null && microphone is null)
        {
            SetStatus("Turn on at least one source.", StatusKind.Warning);
            return;
        }

        string path;
        try
        {
            Directory.CreateDirectory(OutputFolder);
            path = Path.Combine(OutputFolder, $"Memo_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus($"Cannot create the output folder: {ex.Message}", StatusKind.Error);
            return;
        }

        var sources = new List<CaptureSource>();
        if (systemSource is not null)
        {
            systemSource.Gain = (float)SystemAudioGain;
            _systemSource = systemSource;
            sources.Add(systemSource);
        }
        if (microphone is not null)
        {
            _microphoneSource = CaptureSource.ForDevice(_devices, SourceKind.Microphone, microphone.Id);
            _microphoneSource.Gain = (float)MicrophoneGain;
            _microphoneSource.Muted = IsMicrophoneMuted;
            sources.Add(_microphoneSource);
        }
        foreach (var source in sources)
            source.Faulted += OnSourceFaulted;

        try
        {
            _recorder = new Recorder(path, sources);
            _recorder.Faulted += OnRecorderFaulted;
            await _recorder.StartAsync();
        }
        catch (Exception ex)
        {
            if (_recorder is null)
                sources.ForEach(s => s.Dispose());
            ReleaseRecorder(out _);
            TryDelete(path);
            SetStatus(DescribeStartError(ex), StatusKind.Error, "Couldn't start recording");
            return;
        }

        Elapsed = TimeSpan.Zero;
        State = RecorderState.Recording;
        _nextRetry = DateTime.UtcNow + RetryInterval;
        _meterTimer.Start();
        SetStatus(string.Empty, StatusKind.Info);
    }

    [RelayCommand(CanExecute = nameof(IsActive))]
    private void PauseResume()
    {
        if (_recorder is null)
            return;
        State = State == RecorderState.Recording ? RecorderState.Paused : RecorderState.Recording;
        _recorder.IsPaused = State == RecorderState.Paused;
    }

    [RelayCommand(CanExecute = nameof(IsActive))]
    private async Task StopAsync()
    {
        _meterTimer.Stop();
        string? wavPath = ReleaseRecorder(out TimeSpan duration);
        if (wavPath is null)
        {
            State = RecorderState.Idle;
            return;
        }

        if (duration == TimeSpan.Zero)
        {
            TryDelete(wavPath);
            State = RecorderState.Idle;
            SetStatus("Nothing was recorded, so no file was saved.", StatusKind.Info);
            return;
        }

        if (Format == OutputFormat.Wav)
        {
            Complete(wavPath, duration);
            return;
        }

        State = RecorderState.Saving;
        string m4aPath = Path.ChangeExtension(wavPath, ".m4a");
        int bitrate = Bitrate;
        try
        {
            await Task.Run(() => AudioEncoder.EncodeToM4a(wavPath, m4aPath, bitrate));
            TryDelete(wavPath);
            Complete(m4aPath, duration);
        }
        catch (Exception ex)
        {
            TryDelete(m4aPath);
            LastFilePath = wavPath;
            State = RecorderState.Idle;
            SetStatus($"M4A conversion failed ({ex.Message}). The recording was kept as {Path.GetFileName(wavPath)}.",
                StatusKind.Warning, "Saved as WAV", hasFileActions: true);
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose where to save recordings",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : string.Empty,
        };
        if (dialog.ShowDialog() == true)
            OutputFolder = dialog.FolderName;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(OutputFolder);
            Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            SetStatus($"Cannot open the folder: {ex.Message}", StatusKind.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowLastFile))]
    private void ShowLastFile()
    {
        if (EnsureLastFileExists())
            Process.Start("explorer.exe", $"/select,\"{LastFilePath}\"");
    }

    [RelayCommand(CanExecute = nameof(CanShowLastFile))]
    private void OpenLastFile()
    {
        if (!EnsureLastFileExists())
            return;
        try
        {
            Process.Start(new ProcessStartInfo(LastFilePath!) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            SetStatus($"Cannot open the recording: {ex.Message}", StatusKind.Error);
        }
    }

    [RelayCommand]
    private void DismissStatus() => SetStatus(string.Empty, StatusKind.Info);

    private bool CanShowLastFile() => LastFilePath is not null;

    private bool EnsureLastFileExists()
    {
        if (LastFilePath is null)
            return false;
        if (File.Exists(LastFilePath))
            return true;

        SetStatus($"{Path.GetFileName(LastFilePath)} no longer exists.", StatusKind.Warning);
        LastFilePath = null;
        return false;
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void RefreshApps()
    {
        string? preferred = SelectedApp?.ProcessName ?? _chosenAppName;
        var apps = AudioApps.GetRunningApps(_devices.GetAudioSessionProcessIds()).ToList();

        // Keep the saved choice selectable even when the app is not running yet.
        if (preferred is not null && !apps.Any(a => string.Equals(a.ProcessName, preferred, StringComparison.OrdinalIgnoreCase)))
            apps.Insert(0, new AudioApp(preferred, preferred));

        Apps.Clear();
        foreach (var app in apps)
            Apps.Add(app);
        SelectedApp = apps.FirstOrDefault(a => string.Equals(a.ProcessName, preferred, StringComparison.OrdinalIgnoreCase));
    }

    partial void OnSelectedAppChanged(AudioApp? value)
    {
        if (value is not null)
            _chosenAppName = value.ProcessName;
    }

    partial void OnSystemAudioGainChanged(double value)
    {
        if (_systemSource is not null)
            _systemSource.Gain = (float)value;
    }

    partial void OnMicrophoneGainChanged(double value)
    {
        if (_microphoneSource is not null)
            _microphoneSource.Gain = (float)value;
    }

    partial void OnIsMicrophoneMutedChanged(bool value)
    {
        if (_microphoneSource is not null)
            _microphoneSource.Muted = value;
    }

    partial void OnSelectedSystemAudioDeviceChanged(AudioDevice? value)
    {
        if (!_isReloadingDevices && value is not null)
            _chosenSystemDeviceId = value.Id;
    }

    partial void OnSelectedMicrophoneChanged(AudioDevice? value)
    {
        if (!_isReloadingDevices && value is not null)
            _chosenMicrophoneId = value.Id;
    }

    private void OnMeterTick(object? sender, EventArgs e)
    {
        SystemAudioLevel = NextLevel(SystemAudioLevel, _systemSource);
        MicrophoneLevel = NextLevel(MicrophoneLevel, _microphoneSource);
        if (_recorder is not null)
            Elapsed = _recorder.Duration;

        if (DateTime.UtcNow >= _nextRetry)
        {
            _nextRetry = DateTime.UtcNow + RetryInterval;
            _ = RestartLostSourcesAsync();
        }
    }

    private static double NextLevel(double current, CaptureSource? source)
    {
        if (source is null)
            return 0;

        const double floorDb = -60;
        float peak = source.ReadPeak();
        double level = peak > 0 ? Math.Clamp((20 * Math.Log10(peak) - floorDb) / -floorDb, 0, 1) : 0;
        return Math.Max(level, current - 0.03);
    }

    /// <summary>Reconnects sources whose device dropped out (unplugged headset, Bluetooth reconnect, format change).</summary>
    private async Task RestartLostSourcesAsync()
    {
        var recorder = _recorder;
        if (recorder is null || _isRestarting)
            return;

        _isRestarting = true;
        try
        {
            foreach (var source in recorder.Sources)
            {
                if (source.IsRunning || recorder != _recorder)
                    continue;
                try
                {
                    await source.StartAsync();
                    SetStatus($"{Describe(source.Kind)} reconnected.", StatusKind.Success);
                }
                catch (Exception)
                {
                    // Device still unavailable; retry on the next interval.
                }
            }
        }
        finally
        {
            _isRestarting = false;
        }
    }

    private void OnSourceFaulted(object? sender, Exception? ex) => _dispatcher.InvokeAsync(() =>
    {
        if (sender is CaptureSource source && _recorder?.Sources.Contains(source) == true)
            SetStatus($"{Describe(source.Kind)} disconnected. Recording silence until it comes back.", StatusKind.Warning);
    });

    private void OnRecorderFaulted(object? sender, Exception ex) => _dispatcher.InvokeAsync(async () =>
    {
        if (!ReferenceEquals(sender, _recorder))
            return;
        await StopAsync();
        SetStatus($"Failed to write the recording, so recording stopped: {ex.Message}", StatusKind.Error);
    });

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // Endpoint notifications arrive in bursts (one per role/flow); coalesce them into one refresh.
        if (_refreshPending)
            return;
        _refreshPending = true;
        _dispatcher.InvokeAsync(() =>
        {
            _refreshPending = false;
            RefreshDevices();
            _ = RestartLostSourcesAsync();
        }, DispatcherPriority.Background);
    }

    private void RefreshDevices()
    {
        _isReloadingDevices = true;
        try
        {
            SelectedSystemAudioDevice = Reload(SystemAudioDevices, DataFlow.Render, _chosenSystemDeviceId);
            SelectedMicrophone = Reload(Microphones, DataFlow.Capture, _chosenMicrophoneId);
        }
        finally
        {
            _isReloadingDevices = false;
        }
    }

    private AudioDevice? Reload(ObservableCollection<AudioDevice> target, DataFlow flow, string? preferredId)
    {
        var devices = _devices.GetDevices(flow);
        target.Clear();
        foreach (var device in devices)
            target.Add(device);

        string? defaultId = _devices.GetDefaultDeviceId(flow);
        return devices.FirstOrDefault(d => d.Id == preferredId)
            ?? devices.FirstOrDefault(d => d.Id == defaultId)
            ?? devices.FirstOrDefault();
    }

    private string? ReleaseRecorder(out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var recorder = _recorder;
        _recorder = null;
        _systemSource = null;
        _microphoneSource = null;
        SystemAudioLevel = 0;
        MicrophoneLevel = 0;
        if (recorder is null)
            return null;

        recorder.Faulted -= OnRecorderFaulted;
        foreach (var source in recorder.Sources)
            source.Faulted -= OnSourceFaulted;
        recorder.Dispose();
        duration = recorder.Duration;
        return recorder.FilePath;
    }

    private void Complete(string path, TimeSpan duration)
    {
        LastFilePath = path;
        State = RecorderState.Idle;
        SetStatus($"{Path.GetFileName(path)}  ·  {duration:hh\\:mm\\:ss}  ·  {FormatSize(new FileInfo(path).Length)}",
            StatusKind.Success, "Recording saved", hasFileActions: true);
    }

    private void SetStatus(string message, StatusKind kind, string title = "", bool hasFileActions = false)
    {
        StatusTitle = title;
        StatusKind = kind;
        HasFileActions = hasFileActions;
        StatusMessage = message;
    }

    private static string FormatSize(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / (1024.0 * 1024):0.#} MB"
        : $"{Math.Max(1, bytes / 1024)} KB";

    /// <summary>"Recordings" next to the exe, or Documents\Memo when the exe folder is read-only (e.g. Program Files).</summary>
    private static string GetDefaultOutputFolder()
    {
        string exeFolder = AppContext.BaseDirectory;
        try
        {
            using (File.Create(Path.Combine(exeFolder, $".memo-write-test-{Guid.NewGuid():N}"), 1, FileOptions.DeleteOnClose))
            {
            }
            return Path.Combine(exeFolder, "Recordings");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Memo");
        }
    }

    private static string Describe(SourceKind kind) => kind switch
    {
        SourceKind.SystemAudio => "System audio device",
        SourceKind.AppAudio => "App audio",
        _ => "Microphone",
    };

    private static string DescribeStartError(Exception ex) => ex.HResult switch
    {
        E_ACCESSDENIED => "Microphone access denied. Allow desktop apps in Settings > Privacy & security > Microphone.",
        AUDCLNT_E_DEVICE_IN_USE => "The audio device is in exclusive use by another app.",
        _ => $"Cannot start recording: {ex.Message}",
    };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
