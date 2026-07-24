using System.IO;
using System.Media;
using System.Text;

namespace DeskTolls.Services;

public sealed class SoundFeedbackService : IDisposable
{
    internal const int SampleRate = 44_100;
    internal const short BitsPerSample = 16;
    internal const short ChannelCount = 1;

    private const double PeakAmplitude = 0.15;
    private const int FadeMilliseconds = 8;

    private readonly object _playbackLock = new();
    private readonly MemoryStream _copyStream;
    private readonly MemoryStream _pasteStream;
    private readonly SoundPlayer _copyPlayer;
    private readonly SoundPlayer _pastePlayer;
    private bool _disposed;

    public SoundFeedbackService()
    {
        _copyStream = new MemoryStream(CreateCopyWaveData(), writable: false);
        _pasteStream = new MemoryStream(CreatePasteWaveData(), writable: false);
        _copyPlayer = new SoundPlayer(_copyStream);
        _pastePlayer = new SoundPlayer(_pasteStream);
        _copyPlayer.Load();
        _pastePlayer.Load();
    }

    public bool TryPlayCopy(out Exception? error)
    {
        return TryPlay(_copyPlayer, out error);
    }

    public bool TryPlayPaste(out Exception? error)
    {
        return TryPlay(_pastePlayer, out error);
    }

    internal static byte[] CreateCopyWaveData()
    {
        return CreateWave([(880d, 45), (1175d, 55)]);
    }

    internal static byte[] CreatePasteWaveData()
    {
        return CreateWave([(659d, 50), (494d, 70)]);
    }

    public void Dispose()
    {
        lock (_playbackLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _copyPlayer.Dispose();
            _pastePlayer.Dispose();
            _copyStream.Dispose();
            _pasteStream.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private bool TryPlay(SoundPlayer player, out Exception? error)
    {
        lock (_playbackLock)
        {
            if (_disposed)
            {
                error = new ObjectDisposedException(nameof(SoundFeedbackService));
                return false;
            }

            try
            {
                player.Play();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception;
                return false;
            }
        }
    }

    private static byte[] CreateWave(IReadOnlyList<(double Frequency, int DurationMilliseconds)> tones)
    {
        var totalSamples = tones.Sum(tone =>
            (int)Math.Round(SampleRate * tone.DurationMilliseconds / 1000d));
        var dataLength = totalSamples * sizeof(short);

        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(ChannelCount);
        writer.Write(SampleRate);
        writer.Write(SampleRate * ChannelCount * BitsPerSample / 8);
        writer.Write((short)(ChannelCount * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);

        foreach (var (frequency, durationMilliseconds) in tones)
        {
            WriteTone(writer, frequency, durationMilliseconds);
        }

        return stream.ToArray();
    }

    private static void WriteTone(BinaryWriter writer, double frequency, int durationMilliseconds)
    {
        var sampleCount = (int)Math.Round(SampleRate * durationMilliseconds / 1000d);
        var fadeSamples = Math.Min(
            sampleCount / 2,
            (int)Math.Round(SampleRate * FadeMilliseconds / 1000d));

        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var fadeIn = Math.Min(1d, (sampleIndex + 1d) / fadeSamples);
            var fadeOut = Math.Min(1d, (sampleCount - sampleIndex) / (double)fadeSamples);
            var envelope = Math.Min(fadeIn, fadeOut);
            var phase = 2d * Math.PI * frequency * sampleIndex / SampleRate;
            var waveform = (Math.Sin(phase) + 0.12d * Math.Sin(phase * 2d)) / 1.12d;
            var sample = (short)Math.Round(short.MaxValue * PeakAmplitude * envelope * waveform);
            writer.Write(sample);
        }
    }
}
