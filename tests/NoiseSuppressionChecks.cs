using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VibeVocalStudio.Audio;
using VibeVocalStudio.Models;
using VibeVocalStudio.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

public static class NoiseSuppressionChecks
{
    private static readonly AudioSettings NoiseOnly = new()
    {
        NoiseSuppressionEnabled = true,
        HighPassEnabled = false,
        GateEnabled = false,
        CompressorEnabled = false
    };

    public static int Run()
    {
        var failures = new List<string>();
        var checks = new List<string>();
        void Check(string name, bool condition)
        {
            checks.Add(name);
            Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
            if (!condition) failures.Add(name);
        }

        var noise = CreateNoise(48000 * 4);
        var filtered = Process(noise, [noise.Length]);
        var irregular = Process(noise, [1, 127, 480, 13, 997, 61, 1024]);
        var noiseReduction = VoiceProcessor.LinearToDb(Rms(noise, 48000) / Math.Max(1e-12, Rms(filtered, 48000)));
        Check("Supressão atenua chiado com zumbido harmônico em pelo menos 10 dB, sem gate", noiseReduction > 10);
        var hissRandom = new Random(417);
        var hiss = Enumerable.Range(0, noise.Length).Select(_ => (float)(0.04 * (hissRandom.NextDouble() * 2 - 1))).ToArray();
        var filteredHiss = Process(hiss, [137, 960, 203]);
        var hissReduction = VoiceProcessor.LinearToDb(Rms(hiss, 48000) / Math.Max(1e-12, Rms(filteredHiss, 48000)));
        Check("Supressão reduz chiado contínuo em pelo menos 10 dB, sem gate", hissReduction > 10);
        Check("Saída permanece finita e limitada", filtered.All(sample => float.IsFinite(sample) && Math.Abs(sample) <= VoiceProcessor.Ceiling));
        Check("Blocos irregulares produzem o mesmo áudio sem perder amostras", filtered.SequenceEqual(irregular));

        using var processor = new VoiceProcessor(48000);
        processor.UpdateSettings(NoiseOnly);
        var original = noise[..777];
        var samples = original.ToArray();
        processor.Process(samples);
        Check("Medidor de entrada continua mostrando o áudio original", Math.Abs(processor.InputPeak - original.Max(sample => Math.Abs(sample))) < 1e-8);
        processor.UpdateSettings(NoiseOnly with { NoiseSuppressionEnabled = false });
        samples = original.ToArray();
        processor.Process(samples);
        Check("Desligar supressão devolve sinal original imediatamente", samples.SequenceEqual(original));
        processor.UpdateSettings(NoiseOnly);
        samples = original.ToArray();
        processor.Process(samples);
        Check("Religar supressão descarta áudio antigo e reinicia o estado", samples.SequenceEqual(Process(original, [original.Length])));
        processor.UpdateSettings(NoiseOnly with { Bypass = true });
        samples = original.ToArray();
        processor.Process(samples);
        Check("Comparar original ignora também a supressão", samples.SequenceEqual(original));
        processor.UpdateSettings(NoiseOnly with { Muted = true });
        samples = noise.ToArray();
        processor.Process(samples);
        Check("Mudo é imediato mesmo com áudio no buffer de supressão", samples.All(sample => sample == 0));

        using var invalidProcessor = new VoiceProcessor(48000);
        invalidProcessor.UpdateSettings(NoiseOnly);
        var invalid = new float[1920];
        invalid[0] = float.NaN;
        invalid[1] = float.PositiveInfinity;
        invalid[2] = float.NegativeInfinity;
        invalidProcessor.Process(invalid);
        Check("Entradas inválidas não contaminam a rede de supressão", invalid.All(float.IsFinite));

        using var tapProcessor = new VoiceProcessor(48000);
        tapProcessor.UpdateSettings(NoiseOnly);
        var tap = new ProcessedSampleProvider(new SignalGenerator(48000, 1) { Gain = 0.1, Frequency = 1000 }, tapProcessor);
        float[] forwarded = [];
        tap.SamplesProcessed += (buffer, offset, count) => forwarded = buffer.AsSpan(offset, count).ToArray();
        var outgoing = new float[2007];
        var count = tap.Read(outgoing, 7, 2000);
        Check("Retorno e cabo recebem o mesmo áudio com supressão", count == 2000 && forwarded.SequenceEqual(outgoing.Skip(7)) && outgoing.Take(7).All(sample => sample == 0));

        var routed = AudioRouting.PrepareInput(new SignalGenerator(44100, 2) { Gain = 0.1, Frequency = 1000 });
        var routedSamples = new float[4800];
        Check("Entrada estéreo a 44,1 kHz chega ao supressor em mono a 48 kHz", routed.WaveFormat.SampleRate == 48000 && routed.WaveFormat.Channels == 1 && routed.Read(routedSamples, 0, routedSamples.Length) == routedSamples.Length);
        using var resampledProcessor = new VoiceProcessor(routed.WaveFormat.SampleRate);
        resampledProcessor.Process(routedSamples);
        Check("Áudio reamostrado é processado sem valores inválidos", routedSamples.All(float.IsFinite));

        var settingsFolder = Path.Combine(Path.GetTempPath(), "VibeVocal-noise-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsFolder);
        try
        {
            var store = new SettingsStore(settingsFolder);
            File.WriteAllText(Path.Combine(settingsFolder, "settings.json"), "{\"Audio\":{\"InputGainDb\":20,\"OutputGainDb\":-9,\"GateThresholdDb\":-40}}");
            var migrated = store.Load();
            Check("Configuração antiga ativa supressão preservando ganhos e gate", migrated.Audio is { NoiseSuppressionEnabled: true, InputGainDb: 20, OutputGainDb: -9, GateThresholdDb: -40 });
            store.Save(migrated with { Audio = migrated.Audio with { NoiseSuppressionEnabled = false } });
            Check("Escolha de desligar supressão persiste", !store.Load().Audio.NoiseSuppressionEnabled);
        }
        finally
        {
            File.Delete(Path.Combine(settingsFolder, "settings.json"));
            Directory.Delete(settingsFolder);
        }

        using var performanceProcessor = new VoiceProcessor(48000);
        var performanceSamples = new float[960];
        var durations = new List<double>();
        for (var index = 0; index < 1100; index++)
        {
            noise.AsSpan(index % 100 * 960, 960).CopyTo(performanceSamples);
            var started = Stopwatch.GetTimestamp();
            performanceProcessor.Process(performanceSamples);
            if (index >= 100) durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        durations.Sort();
        var averageMs = durations.Average();
        var p99Ms = durations[(int)(durations.Count * 0.99)];
        Check("Supressão cabe em tempo real nos blocos de 20 ms", averageMs < 10 && p99Ms < 20);
        Console.WriteLine($"Ruído isolado: redução {noiseReduction:0.0} dB; CPU média {averageMs:0.000} ms, p99 {p99Ms:0.000} ms por bloco de 20 ms");
        Console.WriteLine($"Chiado sem tons harmônicos: redução {hissReduction:0.0} dB");

        object? speechReport = null;
        var speechPath = Path.Combine("artifacts", "noise-test-speech.wav");
        if (File.Exists(speechPath))
            speechReport = CheckSpeech(speechPath, Check);
        else
            Check("Amostra de fala sintética disponível; execute tests/New-NoiseTestSpeech.ps1", false);

        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/noise-validation.json", JsonSerializer.Serialize(new
        {
            NoiseReductionDb = noiseReduction,
            HissReductionDb = hissReduction,
            AverageProcessingMs = averageMs,
            P99ProcessingMs = p99Ms,
            Speech = speechReport,
            Checks = checks,
            Failures = failures
        }, new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }

    private static object CheckSpeech(string path, Action<string, bool> check)
    {
        using var reader = new WaveFileReader(path);
        var source = AudioRouting.PrepareInput(reader.ToSampleProvider());
        var voice = new List<float>();
        var buffer = new float[4800];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            voice.AddRange(buffer.Take(read));
        var normalization = 0.1 / Rms(voice.ToArray(), 0);
        var clean = new float[voice.Count + 96000];
        for (var index = 0; index < voice.Count; index++)
            clean[index + 48000] = (float)(voice[index] * normalization);
        var noise = CreateNoise(clean.Length);
        var mixed = clean.Zip(noise, (speech, background) => speech + background).ToArray();
        var processed = Process(mixed, [137, 960, 203]);
        var processedClean = Process(clean, [137, 960, 203]);
        const int delay = 1440;
        var referenceEnergy = 0d;
        var inputError = 0d;
        var outputError = 0d;
        var outputEnergy = 0d;
        var correlation = 0d;
        var addedNoiseEnergy = 0d;
        var filteredCleanEnergy = 0d;
        var activeFrames = 0;
        for (var start = 48000; start + 480 + delay < clean.Length - 48000; start += 480)
        {
            var energy = 0d;
            for (var index = start; index < start + 480; index++) energy += clean[index] * clean[index];
            if (Math.Sqrt(energy / 480) < 0.025)
                continue;
            activeFrames++;
            for (var index = start; index < start + 480; index++)
            {
                var output = processed[index + delay];
                referenceEnergy += clean[index] * clean[index];
                inputError += Math.Pow(mixed[index] - clean[index], 2);
                outputError += Math.Pow(output - clean[index], 2);
                outputEnergy += output * output;
                correlation += clean[index] * output;
                addedNoiseEnergy += Math.Pow(output - processedClean[index + delay], 2);
                filteredCleanEnergy += Math.Pow(processedClean[index + delay], 2);
            }
        }
        var inputSnr = 10 * Math.Log10(referenceEnergy / inputError);
        var outputSnr = 10 * Math.Log10(referenceEnergy / outputError);
        var similarity = correlation / Math.Sqrt(referenceEnergy * outputEnergy);
        var levelRatio = Math.Sqrt(outputEnergy / referenceEnergy);
        var addedNoiseSnr = 10 * Math.Log10(filteredCleanEnergy / addedNoiseEnergy);
        check("Fala sintética contém trechos ativos suficientes", activeFrames > 100);
        check("Ruído acrescentado à fala diminui frente à voz limpa igualmente processada, sem gate", addedNoiseSnr > inputSnr + 2);
        check("Voz permanece correlacionada e audível após a supressão", similarity > 0.9 && levelRatio > 0.6 && levelRatio < 1.4);
        Console.WriteLine($"Durante fala: SNR {inputSnr:0.0} → {outputSnr:0.0} dB; correlação {similarity:0.000}, nível relativo {levelRatio:0.000}");
        Console.WriteLine($"Ruído acrescentado à fala após tratamento: SNR {addedNoiseSnr:0.0} dB");
        return new { Source = "Voz sintética local do Windows + ruído determinístico", ActiveFrames = activeFrames, AlignmentSamples = delay, InputSnrDb = inputSnr, OutputSnrAgainstUnfilteredReferenceDb = outputSnr, AddedNoiseSnrAgainstProcessedReferenceDb = addedNoiseSnr, Correlation = similarity, LevelRatio = levelRatio };
    }

    private static float[] CreateNoise(int length)
    {
        var random = new Random(417);
        return Enumerable.Range(0, length).Select(index => (float)(0.04 * (random.NextDouble() * 2 - 1)
            + 0.012 * Math.Sin(2 * Math.PI * 120 * index / 48000)
            + 0.006 * Math.Sin(2 * Math.PI * 240 * index / 48000))).ToArray();
    }

    private static float[] Process(float[] source, int[] chunks)
    {
        using var processor = new VoiceProcessor(48000);
        processor.UpdateSettings(NoiseOnly);
        var output = source.ToArray();
        var position = 0;
        var chunk = 0;
        while (position < output.Length)
        {
            var count = Math.Min(chunks[chunk++ % chunks.Length], output.Length - position);
            processor.Process(output.AsSpan(position, count));
            position += count;
        }
        return output;
    }

    private static double Rms(float[] samples, int skip) => Math.Sqrt(samples.Skip(skip).Average(sample => (double)sample * sample));
}
