using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VibeVocalStudio.Audio;
using VibeVocalStudio.Models;
using VibeVocalStudio.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

if (args.Contains("--ui"))
    return UiChecks.Run();
if (args.Contains("--profiles"))
    return ProfileChecks.Run();
if (args.Contains("--monitor"))
    return MonitorChecks.Run();
if (args.Contains("--noise"))
    return NoiseSuppressionChecks.Run();
if (args.Contains("--startup"))
    return StartupChecks.Run();
if (args.Length == 2 && args[0] == "--running-app")
    return RunningAppChecks.Run(int.Parse(args[1]));

var failures = new List<string>();
var neutral = new AudioSettings { NoiseSuppressionEnabled = false, HighPassEnabled = false, GateEnabled = false, CompressorEnabled = false };

void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

float[] Signal(double amplitude, double frequency = 1000, int length = 48000, int rate = 48000) =>
    Enumerable.Range(0, length).Select(index => (float)(amplitude * Math.Sin(2 * Math.PI * frequency * index / rate))).ToArray();

double Rms(float[] samples, int start = 24000) => Math.Sqrt(samples.Skip(start).Average(sample => (double)sample * sample));

float[] Process(AudioSettings settings, double amplitude = 0.1, double frequency = 1000)
{
    using var processor = new VoiceProcessor(48000);
    processor.UpdateSettings(settings);
    var samples = Signal(amplitude, frequency);
    processor.Process(samples);
    return samples;
}

if (args.Contains("--hardware"))
{
    var service = new AudioDeviceService();
    var inputs = service.GetInputs().Where(device => !AudioDeviceService.IsCable(device.Name)).ToArray();
    var input = inputs.FirstOrDefault(device => device.Id == service.GetDefaultCaptureId()) ?? inputs.First();
    var output = service.GetCableOutputs().First();
    var cableCaptureDevice = service.GetInputs().First(device => AudioDeviceService.IsCable(device.Name));
    using var enumerator = new MMDeviceEnumerator();
    using var captureDevice = enumerator.GetDevice(cableCaptureDevice.Id);
    using var cableCapture = new WasapiCapture(captureDevice);
    long receivedBytes = 0;
    double cablePeak = 0;
    cableCapture.DataAvailable += (_, eventArgs) =>
    {
        Interlocked.Add(ref receivedBytes, eventArgs.BytesRecorded);
        if (cableCapture.WaveFormat.BitsPerSample == 32 && cableCapture.WaveFormat.Encoding is WaveFormatEncoding.IeeeFloat or WaveFormatEncoding.Extensible)
        {
            for (var index = 0; index + 4 <= eventArgs.BytesRecorded; index += 4)
                cablePeak = Math.Max(cablePeak, Math.Abs(BitConverter.ToSingle(eventArgs.Buffer, index)));
        }
    };
    cableCapture.StartRecording();
    using var engine = new AudioEngine();
    string? error = null;
    engine.Failed += message => error = message;
    var runs = new List<object>();
    foreach (var bufferMs in new[] { 40, 20, 80 })
    {
        error = null;
        var previousReceivedBytes = Interlocked.Read(ref receivedBytes);
        var timer = Stopwatch.StartNew();
        var peakInput = 0d;
        var peakOutput = 0d;
        var maxQueue = 0d;
        var maxProcessing = 0d;
        try
        {
            engine.Start(input.Id, output.Id, bufferMs, neutral with { NoiseSuppressionEnabled = true });
        }
        catch (Exception exception)
        {
            Check($"Inicialização com buffer {bufferMs} ms: {exception.Message}", false);
            runs.Add(new { BufferMs = bufferMs, Error = exception.ToString() });
            continue;
        }
        AudioMetrics? last = null;
        while (timer.Elapsed.TotalSeconds < 3)
        {
            Thread.Sleep(25);
            last = engine.Metrics;
            peakInput = Math.Max(peakInput, last.InputPeak);
            peakOutput = Math.Max(peakOutput, last.OutputPeak);
            maxQueue = Math.Max(maxQueue, last.QueueMilliseconds);
            maxProcessing = Math.Max(maxProcessing, last.ProcessingMilliseconds);
        }
        var currentReceived = Interlocked.Read(ref receivedBytes) - previousReceivedBytes;
        Check($"Microfone captura e VB-CABLE recebe com buffer {bufferMs} ms", error is null && last!.CapturedBytes > 0 && currentReceived > 0);
        runs.Add(new { BufferMs = bufferMs, engine.FormatDescription, Last = last, MaxInput = peakInput, MaxOutput = peakOutput, MaxQueueMs = maxQueue, MaxProcessingMs = maxProcessing, CableBytes = currentReceived, Error = error });
        engine.UpdateSettings(neutral with { NoiseSuppressionEnabled = true, Muted = true });
        Thread.Sleep(200);
        Check($"Mudo real com buffer {bufferMs} ms", engine.Metrics.OutputPeak == 0);
        engine.Stop();
    }
    cableCapture.StopRecording();
    var report = new { Input = input, Output = output, CableCapture = cableCaptureDevice, CablePeak = cablePeak, Runs = runs, Failures = failures };
    Directory.CreateDirectory("artifacts");
    File.WriteAllText("artifacts/hardware-validation.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return failures.Count == 0 ? 0 : 1;
}

var baseline = Process(neutral);
Check("Ganho neutro preserva o sinal", Math.Abs(Rms(baseline) - 0.1 / Math.Sqrt(2)) < 0.00001);
var amplified = Process(neutral with { InputGainDb = 6 });
Check("Ganho de entrada +6 dB", Math.Abs(Rms(amplified) / Rms(baseline) - Math.Pow(10, 6d / 20)) < 0.001);
var attenuated = Process(neutral with { OutputGainDb = -12 });
Check("Ganho de saída -12 dB", Math.Abs(Rms(attenuated) / Rms(baseline) - Math.Pow(10, -12d / 20)) < 0.001);
Check("Mudo produz zeros", Process(neutral with { Muted = true }).All(sample => sample == 0));
Check("Bypass ignora ganhos e efeitos", Math.Abs(Rms(Process(new AudioSettings { Bypass = true, InputGainDb = 20, BassDb = 12 })) - Rms(baseline)) < 0.00001);
Check("Limitador segura picos em -1 dBFS", Process(neutral with { InputGainDb = 24 }, 0.9).All(sample => Math.Abs(sample) <= VoiceProcessor.Ceiling + 0.000001));
Check("Limitador continua no bypass", Process(neutral with { Bypass = true }, 2).All(sample => Math.Abs(sample) <= VoiceProcessor.Ceiling + 0.000001));
Check("Gate fecha abaixo do limiar", Rms(Process(neutral with { GateEnabled = true, GateThresholdDb = -35 }, 0.001)) < 0.00002);
Check("Gate preserva fala acima do limiar", Rms(Process(neutral with { GateEnabled = true, GateThresholdDb = -48 })) > 0.06);
Check("Compressor reduz sinal forte", Rms(Process(neutral with { CompressorEnabled = true, CompressorThresholdDb = -24, CompressorRatio = 4 }, 0.6)) < 0.15);
Check("Filtro de 80 Hz reduz graves de 20 Hz", Rms(Process(neutral with { HighPassEnabled = true }, 0.1, 20)) < Rms(Process(neutral, 0.1, 20)) / 8);
Check("Equalização de presença atua em 3 kHz", Rms(Process(neutral with { PresenceDb = 6 }, 0.1, 3000)) > Rms(Process(neutral, 0.1, 3000)) * 1.8);
using var processorCheck = new VoiceProcessor(48000);
processorCheck.UpdateSettings(neutral);
float[] invalidSamples = [float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0];
processorCheck.Process(invalidSamples);
Check("Entradas não finitas não contaminam o áudio", invalidSamples.All(float.IsFinite));
Check("Configuração inválida é normalizada", new AudioSettings { InputGainDb = double.NaN, CompressorRatio = 0 }.Sanitize() is { InputGainDb: 0, CompressorRatio: 1 });

var stereo = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = false };
float[] stereoSamples = [0.2f, 0.6f, -0.4f, -0.8f];
var bytes = new byte[stereoSamples.Length * 4];
Buffer.BlockCopy(stereoSamples, 0, bytes, 0, bytes.Length);
stereo.AddSamples(bytes, 0, bytes.Length);
var mono = new MonoSampleProvider(stereo.ToSampleProvider());
var monoBuffer = new float[2];
Check("Conversão estéreo para mono", mono.Read(monoBuffer, 0, 2) == 2 && Math.Abs(monoBuffer[0] - 0.4) < 0.00001 && Math.Abs(monoBuffer[1] + 0.6) < 0.00001);

var signalGenerator = new SignalGenerator(44100, 1) { Frequency = 1000, Gain = 0.1, Type = SignalGeneratorType.Sin };
var resampled = new WdlResamplingSampleProvider(signalGenerator, 48000);
var converted = new float[4800];
Check("Reamostragem de 44,1 para 48 kHz", resampled.Read(converted, 0, converted.Length) == converted.Length && converted.All(float.IsFinite) && converted.Any(sample => Math.Abs(sample) > 0.09));

var settingsDirectory = Path.Combine(Path.GetTempPath(), "VibeVocal-test-" + Guid.NewGuid().ToString("N"));
using var tapProcessor = new VoiceProcessor(48000);
tapProcessor.UpdateSettings(neutral with { InputGainDb = 6 });
var tappedProvider = new ProcessedSampleProvider(new SignalGenerator(48000, 1) { Gain = 0.1, Frequency = 1000 }, tapProcessor);
float[] copiedSamples = [];
tappedProvider.SamplesProcessed += (samples, offset, count) => copiedSamples = samples.AsSpan(offset, count).ToArray();
var routedSamples = new float[48007];
var routedCount = tappedProvider.Read(routedSamples, 7, 48000);
Check("Retorno recebe cópia do mesmo áudio tratado enviado ao cabo", routedCount == 48000 && copiedSamples.SequenceEqual(routedSamples.Skip(7)) && Math.Abs(Rms(copiedSamples) / Rms(baseline) - Math.Pow(10, 6d / 20)) < 0.001);
tapProcessor.UpdateSettings(neutral with { Muted = true });
tappedProvider.Read(routedSamples, 7, 48000);
Check("Mudo também zera o áudio copiado para retorno", copiedSamples.All(sample => sample == 0));

var settingsStore = new SettingsStore(settingsDirectory);
settingsStore.Save(new StudioSettings { Audio = neutral with { InputGainDb = 6, Muted = true, Bypass = true }, BufferMilliseconds = 20, MonitorDeviceId = "monitor-test" });
var restored = settingsStore.Load();
Check("Ajustes persistem sem restaurar mudo e bypass", restored.Audio.InputGainDb == 6 && !restored.Audio.Muted && !restored.Audio.Bypass && restored.BufferMilliseconds == 20);
Check("Seleção do fone persiste", restored.MonitorDeviceId == "monitor-test");
File.Delete(Path.Combine(settingsDirectory, "settings.json"));
Directory.Delete(settingsDirectory);

using var performanceProcessor = new VoiceProcessor(48000);
var performanceBuffer = Signal(0.1, length: 960);
var watch = Stopwatch.StartNew();
for (var block = 0; block < 1000; block++) performanceProcessor.Process(performanceBuffer);
watch.Stop();
Console.WriteLine($"DSP: {watch.Elapsed.TotalMilliseconds / 1000:0.000} ms/bloco de 20 ms");
Check("Processamento cabe no orçamento de tempo real", watch.Elapsed.TotalMilliseconds / 1000 < 20);
Console.WriteLine($"Resultado: {failures.Count} falhas");
return failures.Count == 0 ? 0 : 1;
