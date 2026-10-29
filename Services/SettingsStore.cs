using System.IO;
using System.Text.Json;
using VibeVocalStudio.Models;

namespace VibeVocalStudio.Services;

public sealed class SettingsStore
{
    private readonly string path;
    private readonly string profilePath;
    private readonly string[] legacyPaths;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeVocalStudio");

    public SettingsStore(string? directory = null, IEnumerable<string>? legacyPaths = null)
    {
        path = Path.Combine(directory ?? DefaultDirectory, "settings.json");
        profilePath = Path.Combine(directory ?? DefaultDirectory, "voice-profile.json");
        this.legacyPaths = legacyPaths?.ToArray() ?? (directory is null ? FindLegacyPaths() : []);
    }

    public StudioSettings Load()
    {
        if (File.Exists(path))
            return ReadSettings(path);
        var candidates = legacyPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var settings = ReadSettings(candidate);
                Save(settings);
                return settings;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                lastError = exception;
            }
        }
        if (lastError is not null)
            throw new IOException("Não foi possível importar os ajustes anteriores.", lastError);
        return new StudioSettings();
    }

    private static StudioSettings ReadSettings(string source)
    {
        var settings = JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(source))
            ?? throw new InvalidDataException("Arquivo de ajustes vazio.");
        return settings with
        {
            BufferMilliseconds = settings.BufferMilliseconds is 20 or 40 or 80 ? settings.BufferMilliseconds : 40,
            Audio = (settings.Audio ?? new AudioSettings()).Sanitize() with { Muted = false, Bypass = false }
        };
    }

    public void Save(StudioSettings settings)
    {
        WriteFile(path, settings);
    }

    public VoiceProfile? LoadProfile()
    {
        if (!File.Exists(profilePath))
            return null;
        var profile = JsonSerializer.Deserialize<VoiceProfile>(File.ReadAllText(profilePath));
        if (profile?.Audio is null)
            throw new InvalidDataException("O perfil de voz está inválido.");
        return profile with { Audio = profile.Audio.Sanitize() with { Muted = false, Bypass = false } };
    }

    public VoiceProfile SaveProfile(AudioSettings audio)
    {
        var profile = new VoiceProfile(DateTimeOffset.Now, audio.Sanitize() with { Muted = false, Bypass = false });
        WriteFile(profilePath, profile);
        return profile;
    }

    private static void WriteFile<T>(string destination, T settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporaryPath = destination + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, Options));
        File.Move(temporaryPath, destination, true);
    }

    private static string[] FindLegacyPaths()
    {
        var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "Data", "settings.json") };
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VibeVocalStudio.csproj")))
            {
                candidates.Add(Path.Combine(current.FullName, "dist", "Data", "settings.json"));
                var binaries = Path.Combine(current.FullName, "bin");
                if (Directory.Exists(binaries))
                    candidates.AddRange(Directory.EnumerateFiles(binaries, "settings.json", SearchOption.AllDirectories)
                        .Where(candidate => Path.GetFileName(Path.GetDirectoryName(candidate)) == "Data"));
                break;
            }
            current = current.Parent;
        }
        return candidates.ToArray();
    }
}
