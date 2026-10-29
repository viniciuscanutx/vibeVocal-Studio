using System.IO;
using Microsoft.Win32;

namespace VibeVocalStudio.Services;

public interface IStartupRegistration
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

public sealed class StartupRegistration(string? executablePath = null) : IStartupRegistration
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "vibeVocal Studio";
    private readonly string executable = Path.GetFullPath(executablePath ?? Path.Combine(AppContext.BaseDirectory, "vibeVocal Studio.exe"));

    public string Command => $"\"{executable}\" --startup";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled && !File.Exists(executable))
            throw new FileNotFoundException("O executável do vibeVocal Studio não foi encontrado.", executable);
        if (enabled && Command.Length > 260)
            throw new InvalidOperationException("Mova o aplicativo para uma pasta com um caminho mais curto para iniciar com o Windows.");
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, true);
        if (enabled)
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, false);
    }
}
