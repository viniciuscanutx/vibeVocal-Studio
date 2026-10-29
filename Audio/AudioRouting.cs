using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VibeVocalStudio.Audio;

public static class AudioRouting
{
    public static ISampleProvider PrepareInput(ISampleProvider source)
    {
        ISampleProvider mono = new MonoSampleProvider(source);
        return mono.WaveFormat.SampleRate == NoiseSuppressor.SampleRate
            ? mono
            : new WdlResamplingSampleProvider(mono, NoiseSuppressor.SampleRate);
    }

    public static ISampleProvider MatchDevice(ISampleProvider source, MMDevice device)
    {
        WaveFormat format;
        using (var client = device.AudioClient)
            format = client.MixFormat;
        ISampleProvider routed = source;
        if (routed.WaveFormat.SampleRate != format.SampleRate)
            routed = new WdlResamplingSampleProvider(routed, format.SampleRate);
        if (format.Channels > 1)
        {
            var channels = new MultiplexingSampleProvider([routed], format.Channels);
            for (var channel = 0; channel < format.Channels; channel++)
                channels.ConnectInputToOutput(0, channel);
            routed = channels;
        }
        return routed;
    }
}
