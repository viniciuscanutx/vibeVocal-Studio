using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VibeVocalStudio.Audio;
using VibeVocalStudio.Services;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

public static class RunningAppChecks
{
    public static int Run(int processId)
    {
        using var process = System.Diagnostics.Process.GetProcessById(processId);
        var executable = process.MainModule!.FileName;
        var devices = new AudioDeviceService();
        var settings = new SettingsStore().Load();
        using var enumerator = new MMDeviceEnumerator();
        using var cable = enumerator.GetDevice(settings.OutputDeviceId ?? devices.GetCableOutputs().First().Id);
        using var manager = cable.AudioSessionManager;
        var activeAudio = false;
        for (var index = 0; index < manager.Sessions.Count; index++)
        {
            using var session = manager.Sessions[index];
            if (session.GetProcessID == processId && session.State == AudioSessionState.AudioSessionStateActive)
                activeAudio = true;
        }
        var report = new
        {
            ProcessId = processId,
            Executable = executable,
            WindowHidden = process.MainWindowHandle == nint.Zero,
            ActiveCableSession = activeAudio,
            NoiseLibraryLoaded = process.Modules.Cast<ProcessModule>().Any(module => module.ModuleName.Equals("rnnoise.dll", StringComparison.OrdinalIgnoreCase)),
            RegisteredWithWindows = new StartupRegistration(executable).IsEnabled,
            settings.AutoStartAudio,
            settings.StartInTray
        };
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/running-app-validation.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report.WindowHidden && report.ActiveCableSession && report.NoiseLibraryLoaded && report.RegisteredWithWindows && report.AutoStartAudio && report.StartInTray ? 0 : 1;
    }
}
