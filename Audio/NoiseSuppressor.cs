namespace VibeVocalStudio.Audio;

public sealed class NoiseSuppressor : IDisposable
{
    public const int SampleRate = 48000;
    public const int FrameSize = 480;

    private readonly float[] inputFrame = new float[FrameSize];
    private readonly float[] outputFrame = new float[FrameSize];
    private NativeNoiseState? denoiser = CreateDenoiser();
    private int position;
    private bool disposed;

    public void Process(Span<float> samples, bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!enabled)
        {
            Reset();
            return;
        }
        denoiser ??= CreateDenoiser();
        for (var index = 0; index < samples.Length; index++)
        {
            inputFrame[position] = samples[index] * 32768f;
            samples[index] = outputFrame[position] / 32768f;
            position++;
            if (position != FrameSize)
                continue;
            denoiser.Process(inputFrame, outputFrame);
            position = 0;
        }
    }

    private void Reset()
    {
        if (denoiser is null)
            return;
        denoiser.Dispose();
        denoiser = null;
        position = 0;
        Array.Clear(inputFrame);
        Array.Clear(outputFrame);
    }

    private static NativeNoiseState CreateDenoiser()
    {
        try
        {
            return NativeNoiseState.Create();
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            throw new InvalidOperationException("Não foi possível carregar a supressão de ruído. Abra o aplicativo dentro da pasta completa, incluindo o arquivo rnnoise.dll.", exception);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        Reset();
        disposed = true;
    }
}
