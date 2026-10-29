using VibeVocalStudio.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VibeVocalStudio.Audio;

public sealed class AudioEngine : IDisposable
{
    private MMDeviceEnumerator? enumerator;
    private MMDevice? inputDevice;
    private MMDevice? outputDevice;
    private WasapiCapture? capture;
    private WasapiOut? playback;
    private BufferedWaveProvider? buffer;
    private VoiceProcessor? processor;
    private AudioMonitor? monitor;
    private WaveFormat? processedFormat;
    private int activeBufferMilliseconds;
    private AudioMetrics metrics = new(0, 0, 0, false, false, 0, 0, 0, 0);
    private long capturedBytes;
    private int droppedBuffers;
    private int queueLimitMilliseconds;
    private volatile bool stopping;
    private int failureReported;

    public bool IsRunning { get; private set; }
    public string FormatDescription { get; private set; } = "";
    public AudioMetrics Metrics => Volatile.Read(ref metrics);
    public event Action<string>? Failed;
    public event Action<string>? MonitorFailed;
    public bool IsMonitoring => Volatile.Read(ref monitor) is not null;
    public long MonitorWrittenSamples => Volatile.Read(ref monitor)?.WrittenSamples ?? 0;

    public void Start(string inputId, string outputId, int bufferMilliseconds, AudioSettings settings)
    {
        Stop();
        stopping = false;
        failureReported = 0;
        capturedBytes = 0;
        droppedBuffers = 0;
        bufferMilliseconds = bufferMilliseconds is 20 or 40 or 80 ? bufferMilliseconds : 40;
        queueLimitMilliseconds = Math.Max(100, bufferMilliseconds * 3);
        activeBufferMilliseconds = bufferMilliseconds;
        try
        {
            enumerator = new MMDeviceEnumerator();
            inputDevice = enumerator.GetDevice(inputId);
            outputDevice = enumerator.GetDevice(outputId);
            if (inputDevice.DataFlow != DataFlow.Capture || outputDevice.DataFlow != DataFlow.Render)
                throw new InvalidOperationException("Selecione uma entrada de microfone e uma saída de reprodução.");
            if (!AudioDeviceService.IsCable(outputDevice.FriendlyName))
                throw new InvalidOperationException("A saída deve ser um dispositivo VB-CABLE.");
            if (AudioDeviceService.IsCable(inputDevice.FriendlyName))
                throw new InvalidOperationException("Selecione o microfone físico na entrada para evitar realimentação pelo cabo.");
            capture = new WasapiCapture(inputDevice, true, bufferMilliseconds);
            buffer = new BufferedWaveProvider(capture.WaveFormat, TimeSpan.FromMilliseconds(Math.Max(500, queueLimitMilliseconds * 2)))
            {
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };
            var mono = AudioRouting.PrepareInput(buffer.ToSampleProvider());
            processor = new VoiceProcessor(mono.WaveFormat.SampleRate);
            processor.UpdateSettings(settings);
            var processed = new ProcessedSampleProvider(mono, processor);
            processed.Measured += UpdateMetrics;
            processed.SamplesProcessed += ForwardToMonitor;
            processedFormat = processed.WaveFormat;
            var routed = AudioRouting.MatchDevice(processed, outputDevice);
            playback = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, bufferMilliseconds);
            playback.Init(routed.ToWaveProvider());
            playback.PlaybackStopped += OnPlaybackStopped;
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
            FormatDescription = $"{capture.WaveFormat.SampleRate / 1000d:0.#} kHz → {routed.WaveFormat.SampleRate / 1000d:0.#} kHz · {bufferMilliseconds} ms de buffer";
            capture.StartRecording();
            playback.Play();
            IsRunning = true;
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void UpdateSettings(AudioSettings settings) => processor?.UpdateSettings(settings);

    public void StartMonitoring(string deviceId)
    {
        StopMonitoring();
        if (!IsRunning || processedFormat is null)
            throw new InvalidOperationException("Inicie o processamento antes de ouvir o microfone.");
        var newMonitor = new AudioMonitor(deviceId, processedFormat, activeBufferMilliseconds);
        newMonitor.Failed += OnMonitorFailed;
        Volatile.Write(ref monitor, newMonitor);
        try
        {
            newMonitor.Start();
        }
        catch
        {
            StopMonitoring();
            throw;
        }
    }

    public void StopMonitoring()
    {
        var previous = Interlocked.Exchange(ref monitor, null);
        if (previous is null)
            return;
        previous.Failed -= OnMonitorFailed;
        previous.Dispose();
    }

    private void ForwardToMonitor(float[] samples, int offset, int count) => Volatile.Read(ref monitor)?.Write(samples, offset, count);
    private void OnMonitorFailed(string message) => MonitorFailed?.Invoke(message);

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (stopping || buffer is null)
            return;
        try
        {
            if (buffer.BufferedDuration.TotalMilliseconds > queueLimitMilliseconds)
            {
                buffer.ClearBuffer();
                Interlocked.Increment(ref droppedBuffers);
            }
            buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
            Interlocked.Add(ref capturedBytes, args.BytesRecorded);
        }
        catch (Exception exception)
        {
            ReportFailure(exception.Message);
        }
    }

    private void UpdateMetrics(AudioMetrics value)
    {
        Volatile.Write(ref metrics, value with
        {
            QueueMilliseconds = buffer?.BufferedDuration.TotalMilliseconds ?? 0,
            CapturedBytes = Interlocked.Read(ref capturedBytes),
            DroppedBuffers = Volatile.Read(ref droppedBuffers)
        });
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (!stopping)
            ReportFailure(args.Exception?.Message ?? "O microfone interrompeu a captura. Reconecte-o e atualize os dispositivos.");
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        if (!stopping)
            ReportFailure(args.Exception?.Message ?? "O VB-CABLE interrompeu a reprodução. Atualize os dispositivos e inicie novamente.");
    }

    private void ReportFailure(string message)
    {
        if (Interlocked.Exchange(ref failureReported, 1) == 0)
            Failed?.Invoke(message);
    }

    public void Stop()
    {
        stopping = true;
        IsRunning = false;
        StopMonitoring();
        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            try { capture.StopRecording(); } catch (System.Runtime.InteropServices.COMException) { }
            capture.Dispose();
            capture = null;
        }
        if (playback is not null)
        {
            playback.PlaybackStopped -= OnPlaybackStopped;
            try { playback.Stop(); } catch (System.Runtime.InteropServices.COMException) { }
            playback.Dispose();
            playback = null;
        }
        processor?.Dispose();
        processor = null;
        processedFormat = null;
        buffer = null;
        inputDevice?.Dispose();
        outputDevice?.Dispose();
        enumerator?.Dispose();
        inputDevice = null;
        outputDevice = null;
        enumerator = null;
        Volatile.Write(ref metrics, new AudioMetrics(0, 0, 0, false, false, 0, 0, 0, 0));
    }

    public void Dispose() => Stop();
}
