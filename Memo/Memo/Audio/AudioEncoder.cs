using NAudio.MediaFoundation;
using NAudio.Wave;

namespace Memo.Audio;

public enum OutputFormat
{
    M4a,
    Wav,
}

internal static class AudioEncoder
{
    public static readonly int[] AacBitrates = [96000, 128000, 160000, 192000];

    /// <summary>Encodes a WAV file to AAC (.m4a) with the Media Foundation encoder built into Windows.</summary>
    public static void EncodeToM4a(string wavPath, string m4aPath, int bitrate)
    {
        MediaFoundationApi.Startup();
        using var reader = new WaveFileReader(wavPath);
        MediaFoundationEncoder.EncodeToAac(reader, m4aPath, bitrate);
    }
}
