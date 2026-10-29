using System.Diagnostics;
using NAudio.Wave;

namespace VibeVocalStudio.Audio;

public sealed class ProcessedSampleProvider(ISampleProvider source, VoiceProcessor processor) : ISampleProvider
{
    public WaveFormat WaveFormat => source.WaveFormat;
    public event Action<AudioMetrics>? Measured;
    public event Action<float[], int, int>? SamplesProcessed;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);
        var started = Stopwatch.GetTimestamp();
        processor.Process(buffer.AsSpan(offset, read));
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        SamplesProcessed?.Invoke(buffer, offset, read);
        Measured?.Invoke(new AudioMetrics(processor.InputPeak, processor.OutputPeak, processor.ReductionDb,
            processor.InputClipped, processor.GateClosed, elapsed, 0, 0, 0));
        return read;
    }
}

public sealed record AudioMetrics(double InputPeak, double OutputPeak, double ReductionDb,
    bool InputClipped, bool GateClosed, double ProcessingMilliseconds, double QueueMilliseconds,
    long CapturedBytes, int DroppedBuffers);
