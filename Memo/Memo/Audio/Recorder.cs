using System.Diagnostics;
using NAudio.Wave;

namespace Memo.Audio;

/// <summary>
/// Mixes capture sources on a wall-clock timeline and writes 48 kHz / 16-bit stereo WAV.
/// Sources that deliver nothing (idle loopback, unplugged device) contribute silence instead of stalling the file.
/// </summary>
internal sealed class Recorder : IDisposable
{
    public static readonly WaveFormat OutputFormat = new(CaptureSource.SampleRate, 16, CaptureSource.Channels);

    private const int TickMilliseconds = 10;
    private const int MaxChunkFrames = CaptureSource.SampleRate / 2;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<CaptureSource> _sources;
    private readonly WaveFileWriter _writer;
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _paused;
    private long _framesWritten;

    public Recorder(string filePath, IReadOnlyList<CaptureSource> sources)
    {
        FilePath = filePath;
        _sources = sources;
        _writer = new WaveFileWriter(filePath, OutputFormat);
    }

    public string FilePath { get; }

    public IReadOnlyList<CaptureSource> Sources => _sources;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)Interlocked.Read(ref _framesWritten) / CaptureSource.SampleRate);

    public bool IsPaused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Raised on the mixer thread when writing fails (e.g. disk full). Recording has stopped.</summary>
    public event EventHandler<Exception>? Faulted;

    public async Task StartAsync()
    {
        foreach (var source in _sources)
            await source.StartAsync();

        _running = true;
        _thread = new Thread(MixLoop)
        {
            IsBackground = true,
            Name = "Memo mixer",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join();
        _thread = null;

        foreach (var source in _sources)
            source.Dispose();

        _writer.Dispose();
    }

    private void MixLoop()
    {
        const int channels = CaptureSource.Channels;
        var mix = new float[MaxChunkFrames * channels];
        var scratch = new float[MaxChunkFrames * channels];
        var pcm = new byte[MaxChunkFrames * channels * sizeof(short)];
        var clock = Stopwatch.StartNew();
        var lastFlush = TimeSpan.Zero;
        long framesProduced = 0;

        try
        {
            while (_running)
            {
                Thread.Sleep(TickMilliseconds);

                long due = (long)(clock.Elapsed.TotalSeconds * CaptureSource.SampleRate) - framesProduced;
                int frames = (int)Math.Min(due, MaxChunkFrames);
                if (frames <= 0)
                    continue;

                var mixSpan = mix.AsSpan(0, frames * channels);
                mixSpan.Clear();
                foreach (var source in _sources)
                    source.MixInto(mixSpan, scratch);
                framesProduced += frames;

                // While paused the sources keep draining so resume picks up live audio, not stale buffers.
                if (_paused)
                    continue;

                for (int i = 0; i < mixSpan.Length; i++)
                {
                    short value = (short)(Math.Clamp(mixSpan[i], -1f, 1f) * short.MaxValue);
                    pcm[i * 2] = (byte)value;
                    pcm[i * 2 + 1] = (byte)(value >> 8);
                }

                _writer.Write(pcm, 0, mixSpan.Length * sizeof(short));
                Interlocked.Add(ref _framesWritten, frames);

                if (clock.Elapsed - lastFlush >= FlushInterval)
                {
                    // Rewrites the RIFF header so the file stays playable if the app is killed.
                    _writer.Flush();
                    lastFlush = clock.Elapsed;
                }
            }
        }
        catch (Exception ex)
        {
            _running = false;
            Faulted?.Invoke(this, ex);
        }
    }
}
