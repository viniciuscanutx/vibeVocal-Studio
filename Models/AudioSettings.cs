namespace VibeVocalStudio.Models;

public sealed record AudioSettings
{
    public double InputGainDb { get; init; }
    public double OutputGainDb { get; init; }
    public bool HighPassEnabled { get; init; } = true;
    public bool NoiseSuppressionEnabled { get; init; } = true;
    public double BassDb { get; init; }
    public double PresenceDb { get; init; }
    public double TrebleDb { get; init; }
    public bool GateEnabled { get; init; } = true;
    public double GateThresholdDb { get; init; } = -48;
    public bool CompressorEnabled { get; init; } = true;
    public double CompressorThresholdDb { get; init; } = -18;
    public double CompressorRatio { get; init; } = 3;
    public bool Bypass { get; init; }
    public bool Muted { get; init; }

    public AudioSettings Sanitize() => this with
    {
        InputGainDb = Clamp(InputGainDb, -24, 24, 0),
        OutputGainDb = Clamp(OutputGainDb, -24, 12, 0),
        BassDb = Clamp(BassDb, -12, 12, 0),
        PresenceDb = Clamp(PresenceDb, -12, 12, 0),
        TrebleDb = Clamp(TrebleDb, -12, 12, 0),
        GateThresholdDb = Clamp(GateThresholdDb, -72, -20, -48),
        CompressorThresholdDb = Clamp(CompressorThresholdDb, -36, -6, -18),
        CompressorRatio = Clamp(CompressorRatio, 1, 8, 3)
    };

    private static double Clamp(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

public sealed record StudioSettings
{
    public bool AutoStartAudio { get; init; }
    public bool StartInTray { get; init; }
    public string? InputDeviceId { get; init; }
    public string? OutputDeviceId { get; init; }
    public string? MonitorDeviceId { get; init; }
    public int BufferMilliseconds { get; init; } = 40;
    public AudioSettings Audio { get; init; } = new();
}

public sealed record VoiceProfile(DateTimeOffset SavedAt, AudioSettings Audio);
