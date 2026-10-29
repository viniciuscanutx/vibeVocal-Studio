using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VibeVocalStudio.Audio;

public sealed class AudioMonitor : IDisposable
{
    private readonly MMDeviceEnumerator enumerator;
    private readonly MMDevice device;
    private readonly BufferedWaveProvider buffer;
    private readonly WasapiOut playback;
    private readonly object bufferLock = new();
    private readonly int maximumQueueMilliseconds;
    private byte[] transferBuffer = [];
    private bool stopped;
    private long writtenSamples;

    public event Action<string>? Failed;
    public long WrittenSamples => Interlocked.Read(ref writtenSamples);

    public AudioMonitor(string deviceId, WaveFormat sourceFormat, int bufferMilliseconds)
    {
        enumerator = new MMDeviceEnumerator();
        try
        {
            device = enumerator.GetDevice(deviceId);
            if (device.DataFlow != DataFlow.Render || AudioDeviceService.IsCable(device.FriendlyName))
                throw new InvalidOperationException("Escolha um fone ou alto-falante para ouvir o microfone.");
            maximumQueueMilliseconds = Math.Max(100, bufferMilliseconds * 3);
            buffer = new BufferedWaveProvider(sourceFormat, TimeSpan.FromMilliseconds(Math.Max(500, maximumQueueMilliseconds * 2)))
            {
                ReadFully = true,
                DiscardOnBufferOverflow = true
            };
            var source = AudioRouting.MatchDevice(buffer.ToSampleProvider(), device);
            playback = new WasapiOut(device, AudioClientShareMode.Shared, true, bufferMilliseconds);
            playback.Init(source.ToWaveProvider());
            playback.PlaybackStopped += OnPlaybackStopped;
        }
        catch
        {
            playback?.Dispose();
            device?.Dispose();
            enumerator.Dispose();
            throw;
        }
    }

    public void Start() => playback.Play();

    public void Write(float[] samples, int offset, int count)
    {
        lock (bufferLock)
        {
            if (stopped || count == 0)
                return;
            var byteCount = count * sizeof(float);
            if (transferBuffer.Length < byteCount)
                transferBuffer = new byte[byteCount];
            Buffer.BlockCopy(samples, offset * sizeof(float), transferBuffer, 0, byteCount);
            if (buffer.BufferedDuration.TotalMilliseconds > maximumQueueMilliseconds)
                buffer.ClearBuffer();
            buffer.AddSamples(transferBuffer, 0, byteCount);
            Interlocked.Add(ref writtenSamples, count);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        lock (bufferLock)
        {
            if (stopped)
                return;
        }
        Failed?.Invoke(args.Exception?.Message ?? "O dispositivo de retorno foi desconectado.");
    }

    public void Dispose()
    {
        lock (bufferLock)
        {
            if (stopped)
                return;
            stopped = true;
        }
        playback.PlaybackStopped -= OnPlaybackStopped;
        try { playback.Stop(); } catch (System.Runtime.InteropServices.COMException) { }
        playback.Dispose();
        device.Dispose();
        enumerator.Dispose();
    }
}
