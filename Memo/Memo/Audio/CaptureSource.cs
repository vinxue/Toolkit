using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace Memo.Audio;

public enum SourceKind
{
    SystemAudio,
    AppAudio,
    Microphone,
}

/// <summary>
/// Captures one WASAPI stream and converts it to 48 kHz stereo float, buffered for the mixer.
/// The resampler rate is nudged continuously so the device clock tracks the mixer's wall clock.
/// </summary>
internal sealed class CaptureSource : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    // Jitter buffer: start playing out once this much is queued.
    private const int TargetSamples = SampleRate / 10 * Channels;
    // Safety net after stalls; drift is otherwise absorbed by rate correction.
    private const int MaxSamples = SampleRate * 4 / 10 * Channels;

    // Rate correction per second of buffer error, capped at 0.3% (about 5 cents, inaudible on speech).
    private const double DriftGain = 0.05;
    private const double MaxCorrection = 0.003;
    private const double FillSmoothing = 0.02;

    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private enum SampleFormat { Float32, Pcm16, Pcm24, Pcm32 }

    private readonly AudioDeviceService _devices;
    private readonly string? _deviceId;
    private readonly uint _processId;
    private readonly FloatRingBuffer _ring = new(SampleRate * Channels * 2);
    private MMDevice? _device;
    private WasapiRecorder? _capture;
    private WdlResampler _resampler = new();
    private SampleFormat _format;
    private int _inChannels;
    private int _inRate;
    private int _blockAlign;
    private double _fillAverage;
    private float[] _converted = [];
    private float[] _resampled = [];
    private float _peak;
    private bool _primed;
    private volatile bool _configured;
    private volatile bool _isRunning;

    private CaptureSource(AudioDeviceService devices, SourceKind kind, string? deviceId, uint processId)
    {
        _devices = devices;
        _deviceId = deviceId;
        _processId = processId;
        Kind = kind;
    }

    public SourceKind Kind { get; }

    public bool IsRunning => _isRunning;

    public float Gain { get; set; } = 1f;

    public bool Muted { get; set; }

    /// <summary>Raised when capture stops without <see cref="Stop"/> being called, e.g. the device was unplugged.</summary>
    public event EventHandler<Exception?>? Faulted;

    public static CaptureSource ForDevice(AudioDeviceService devices, SourceKind kind, string deviceId) =>
        new(devices, kind, deviceId, 0);

    /// <summary>Captures only what the process (and its child processes) plays. Requires Windows 10 2004+.</summary>
    public static CaptureSource ForProcess(AudioDeviceService devices, uint processId) =>
        new(devices, SourceKind.AppAudio, null, processId);

    public async Task StartAsync()
    {
        if (_isRunning)
            return;

        ReleaseCapture();
        try
        {
            var builder = new WasapiRecorderBuilder().WithSharedMode().WithBufferLength(50);
            if (Kind == SourceKind.AppAudio)
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                    throw new PlatformNotSupportedException("Recording a single app requires Windows 10 version 2004 or later.");

                // The process-loopback virtual device has no mix format, so one must be requested.
                builder = builder
                    .WithProcessLoopback(_processId, ProcessLoopbackMode.IncludeTargetProcessTree)
                    .WithFormat(new WaveFormat(SampleRate, 16, Channels));
            }
            else
            {
                _device = _devices.GetDevice(_deviceId!);
                builder = builder.WithDevice(_device);
                if (Kind == SourceKind.SystemAudio)
                    builder = builder.WithLoopbackCapture();
            }

            _configured = false;
            _capture = await builder.BuildAsync();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            Configure(_capture.WaveFormat);
            _isRunning = true;
        }
        catch
        {
            ReleaseCapture();
            throw;
        }
    }

    public void Stop() => ReleaseCapture();

    public void Dispose() => ReleaseCapture();

    /// <summary>Returns the peak level since the last call and resets it.</summary>
    public float ReadPeak() => Interlocked.Exchange(ref _peak, 0f);

    /// <summary>
    /// Adds the next <paramref name="mix"/>.Length samples into <paramref name="mix"/>.
    /// Missing data is treated as silence so the timeline never stalls. Mixer thread only.
    /// </summary>
    public void MixInto(Span<float> mix, Span<float> scratch)
    {
        int available = _ring.Count;
        if (!_primed)
        {
            if (available < TargetSamples)
                return;
            _primed = true;
        }

        if (available > MaxSamples)
            _ring.Discard(available - TargetSamples);

        int read = _ring.Read(scratch[..mix.Length]);
        for (int i = 0; i < read; i++)
            mix[i] += scratch[i];

        if (read < mix.Length)
            _primed = false;
    }

    private void Configure(WaveFormat format)
    {
        _format = ResolveFormat(format);
        _inChannels = format.Channels;
        _inRate = format.SampleRate;
        _blockAlign = format.BlockAlign;
        _fillAverage = TargetSamples;

        // Always resample, even at 48 kHz, so the rate can be trimmed for clock drift.
        _resampler = new WdlResampler();
        _resampler.SetMode(true, 2, false);
        _resampler.SetFilterParms();
        _resampler.SetFeedMode(true);
        _resampler.SetRates(_inRate, SampleRate);
        _configured = true;
    }

    private static SampleFormat ResolveFormat(WaveFormat format)
    {
        Guid? subFormat = (format as WaveFormatExtensible)?.SubFormat;
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat || subFormat == FloatSubFormat;
        bool isPcm = format.Encoding == WaveFormatEncoding.Pcm || subFormat == PcmSubFormat;

        return (isFloat, isPcm, format.BitsPerSample) switch
        {
            (true, _, 32) => SampleFormat.Float32,
            (_, true, 16) => SampleFormat.Pcm16,
            (_, true, 24) => SampleFormat.Pcm24,
            (_, true, 32) => SampleFormat.Pcm32,
            _ => throw new NotSupportedException($"Unsupported audio format: {format}"),
        };
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        // Packets arriving before the format is known are dropped (a few ms at most).
        if (!_configured)
            return;

        int frames = buffer.Length / _blockAlign;
        if (frames == 0)
            return;

        EnsureSize(ref _converted, frames * Channels);
        bool silent = (flags & AudioClientBufferFlags.Silent) != 0;
        ReadOnlySpan<byte> data = buffer[..(frames * _blockAlign)];
        int bytesPerSample = _blockAlign / _inChannels;
        float gain = Muted ? 0f : Gain;
        float peak = 0f;

        if (silent)
        {
            _converted.AsSpan(0, frames * Channels).Clear();
        }
        else
        {
            for (int f = 0; f < frames; f++)
            {
                ReadOnlySpan<byte> frame = data.Slice(f * _blockAlign, _blockAlign);
                float left, right;

                if (Kind == SourceKind.Microphone)
                {
                    // Mic arrays expose several channels of the same voice; fold them to centred mono.
                    float sum = 0f;
                    for (int c = 0; c < _inChannels; c++)
                        sum += ReadSample(frame, c * bytesPerSample);
                    left = right = sum / _inChannels;
                }
                else
                {
                    left = ReadSample(frame, 0);
                    right = _inChannels > 1 ? ReadSample(frame, bytesPerSample) : left;
                }

                left *= gain;
                right *= gain;
                peak = Math.Max(peak, Math.Max(Math.Abs(left), Math.Abs(right)));
                _converted[f * 2] = left;
                _converted[f * 2 + 1] = right;
            }
        }

        if (peak > _peak)
            _peak = peak;

        // Buffer above target means the device runs fast relative to the mixer: emit slightly fewer samples.
        _fillAverage += (_ring.Count - _fillAverage) * FillSmoothing;
        double errorSeconds = (TargetSamples - _fillAverage) / (SampleRate * Channels);
        double correction = Math.Clamp(errorSeconds * DriftGain, -MaxCorrection, MaxCorrection);
        _resampler.SetRates(_inRate, SampleRate * (1 + correction));

        _resampler.ResamplePrepare(frames, Channels, out Span<float> inBuffer);
        _converted.AsSpan(0, frames * Channels).CopyTo(inBuffer);
        int maxOut = (int)((long)frames * SampleRate / _inRate) + 64;
        EnsureSize(ref _resampled, maxOut * Channels);
        int produced = _resampler.ResampleOut(_resampled, frames, maxOut, Channels);
        _ring.Write(_resampled.AsSpan(0, produced * Channels));
    }

    private float ReadSample(ReadOnlySpan<byte> frame, int offset) => _format switch
    {
        SampleFormat.Float32 => BinaryPrimitives.ReadSingleLittleEndian(frame[offset..]),
        SampleFormat.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(frame[offset..]) / 32768f,
        SampleFormat.Pcm24 => (frame[offset] | (frame[offset + 1] << 8) | ((sbyte)frame[offset + 2] << 16)) / 8388608f,
        _ => BinaryPrimitives.ReadInt32LittleEndian(frame[offset..]) / 2147483648f,
    };

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isRunning = false;
        Faulted?.Invoke(this, e.Exception);
    }

    private void ReleaseCapture()
    {
        _isRunning = false;
        _configured = false;
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;
    }

    private static void EnsureSize(ref float[] buffer, int size)
    {
        if (buffer.Length < size)
            buffer = new float[size];
    }
}
