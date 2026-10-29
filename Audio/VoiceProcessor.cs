using VibeVocalStudio.Models;
using NAudio.Dsp;

namespace VibeVocalStudio.Audio;

public sealed class VoiceProcessor : IDisposable
{
    private readonly int sampleRate;
    private readonly NoiseSuppressor noiseSuppressor;
    private AudioSettings settings = new();
    private AudioSettings? appliedSettings;
    private BiQuadFilter highPass;
    private BiQuadFilter bass;
    private BiQuadFilter presence;
    private BiQuadFilter treble;
    private readonly double envelopeAttack;
    private readonly double envelopeRelease;
    private readonly double gateAttack;
    private readonly double gateRelease;
    private readonly double compressorAttack;
    private readonly double compressorRelease;
    private readonly double limiterRelease;
    private readonly double gainSmoothing;
    private double envelope;
    private double gateGain = 1;
    private double compressorGain = 1;
    private double limiterGain = 1;
    private double inputGain = 1;
    private double outputGain = 1;
    private bool gateOpen = true;
    private int holdFrames;

    public const double Ceiling = 0.8912509381337456;
    public double InputPeak { get; private set; }
    public double OutputPeak { get; private set; }
    public double ReductionDb { get; private set; }
    public bool InputClipped { get; private set; }
    public bool GateClosed { get; private set; }

    public VoiceProcessor(int sampleRate)
    {
        if (sampleRate != NoiseSuppressor.SampleRate)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "O processamento de voz requer áudio a 48 kHz.");
        this.sampleRate = sampleRate;
        noiseSuppressor = new NoiseSuppressor();
        highPass = BiQuadFilter.HighPassFilter(sampleRate, 80, 0.707f);
        bass = BiQuadFilter.LowShelf(sampleRate, 160, 1, 0);
        presence = BiQuadFilter.PeakingEQ(sampleRate, Math.Min(3000, sampleRate * 0.3f), 0.8f, 0);
        treble = BiQuadFilter.HighShelf(sampleRate, Math.Min(8000, sampleRate * 0.4f), 1, 0);
        envelopeAttack = Coefficient(0.002);
        envelopeRelease = Coefficient(0.080);
        gateAttack = Coefficient(0.003);
        gateRelease = Coefficient(0.100);
        compressorAttack = Coefficient(0.008);
        compressorRelease = Coefficient(0.120);
        limiterRelease = Coefficient(0.060);
        gainSmoothing = Coefficient(0.010);
    }

    public void UpdateSettings(AudioSettings value) => Volatile.Write(ref settings, value.Sanitize());

    public void Process(Span<float> samples)
    {
        var current = Volatile.Read(ref settings);
        ApplyEqualizer(current);
        var targetInputGain = DbToLinear(current.InputGainDb);
        var targetOutputGain = DbToLinear(current.OutputGainDb);
        var gateThreshold = DbToLinear(current.GateThresholdDb);
        var inputPeak = 0d;
        var outputPeak = 0d;
        var clipped = false;
        for (var index = 0; index < samples.Length; index++)
        {
            var raw = float.IsFinite(samples[index]) ? samples[index] : 0;
            samples[index] = raw;
            inputPeak = Math.Max(inputPeak, Math.Abs(raw));
            clipped |= Math.Abs(raw) >= 0.999;
        }
        noiseSuppressor.Process(samples, current.NoiseSuppressionEnabled && !current.Bypass);
        for (var index = 0; index < samples.Length; index++)
        {
            inputGain = Smooth(inputGain, targetInputGain, gainSmoothing);
            outputGain = Smooth(outputGain, targetOutputGain, gainSmoothing);
            double sample = samples[index];
            if (!current.Bypass)
            {
                sample *= inputGain;
                if (current.HighPassEnabled)
                    sample = highPass.Transform((float)sample);
                sample = bass.Transform((float)sample);
                sample = presence.Transform((float)sample);
                sample = treble.Transform((float)sample);
                var absolute = Math.Abs(sample);
                envelope = Smooth(envelope, absolute, absolute > envelope ? envelopeAttack : envelopeRelease);
                if (envelope >= gateThreshold)
                {
                    gateOpen = true;
                    holdFrames = sampleRate / 10;
                }
                else if (holdFrames > 0)
                    holdFrames--;
                else if (envelope < gateThreshold * 0.708)
                    gateOpen = false;

                var targetGate = !current.GateEnabled || gateOpen ? 1d : 0d;
                gateGain = Smooth(gateGain, targetGate, targetGate > gateGain ? gateAttack : gateRelease);
                sample *= gateGain;
                var aboveThreshold = Math.Max(0, LinearToDb(envelope) - current.CompressorThresholdDb);
                var reduction = current.CompressorEnabled ? aboveThreshold * (1 - 1 / current.CompressorRatio) : 0;
                var targetCompressor = DbToLinear(-reduction);
                compressorGain = Smooth(compressorGain, targetCompressor,
                    targetCompressor < compressorGain ? compressorAttack : compressorRelease);
                sample *= compressorGain * outputGain;
            }
            var targetLimiter = Math.Abs(sample) > Ceiling ? Ceiling / Math.Abs(sample) : 1;
            limiterGain = targetLimiter < limiterGain ? targetLimiter : Smooth(limiterGain, targetLimiter, limiterRelease);
            sample = current.Muted ? 0 : Math.Clamp(sample * limiterGain, -Ceiling, Ceiling);
            samples[index] = (float)sample;
            outputPeak = Math.Max(outputPeak, Math.Abs(sample));
        }
        InputPeak = inputPeak;
        OutputPeak = outputPeak;
        InputClipped = clipped;
        GateClosed = !current.Bypass && current.GateEnabled && gateGain < 0.1;
        ReductionDb = -LinearToDb((current.Bypass ? 1 : compressorGain) * limiterGain);
    }

    public static double DbToLinear(double value) => Math.Pow(10, value / 20);
    public void Dispose() => noiseSuppressor.Dispose();
    public static double LinearToDb(double value) => 20 * Math.Log10(Math.Max(0.000001, value));
    private double Coefficient(double seconds) => Math.Exp(-1 / (sampleRate * seconds));
    private static double Smooth(double previous, double target, double coefficient) => target + coefficient * (previous - target);

    private void ApplyEqualizer(AudioSettings current)
    {
        if (appliedSettings is null || current.BassDb != appliedSettings.BassDb)
            bass = BiQuadFilter.LowShelf(sampleRate, 160, 1, (float)current.BassDb);
        if (appliedSettings is null || current.PresenceDb != appliedSettings.PresenceDb)
            presence = BiQuadFilter.PeakingEQ(sampleRate, Math.Min(3000, sampleRate * 0.3f), 0.8f, (float)current.PresenceDb);
        if (appliedSettings is null || current.TrebleDb != appliedSettings.TrebleDb)
            treble = BiQuadFilter.HighShelf(sampleRate, Math.Min(8000, sampleRate * 0.4f), 1, (float)current.TrebleDb);
        appliedSettings = current;
    }
}
