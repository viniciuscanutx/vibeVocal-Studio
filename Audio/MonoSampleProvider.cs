using NAudio.Wave;

namespace VibeVocalStudio.Audio;

public sealed class MonoSampleProvider(ISampleProvider source) : ISampleProvider
{
    private float[] readBuffer = [];
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = source.WaveFormat.Channels;
        if (channels == 1)
            return source.Read(buffer, offset, count);
        var needed = count * channels;
        if (readBuffer.Length < needed)
            readBuffer = new float[needed];
        var read = source.Read(readBuffer, 0, needed);
        var frames = read / channels;
        for (var frame = 0; frame < frames; frame++)
        {
            double sum = 0;
            for (var channel = 0; channel < channels; channel++)
                sum += readBuffer[frame * channels + channel];
            buffer[offset + frame] = (float)(sum / channels);
        }
        return frames;
    }
}
