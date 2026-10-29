using VibeVocalStudio.Models;
using NAudio.CoreAudioApi;

namespace VibeVocalStudio.Audio;

public sealed class AudioDeviceService
{
    public IReadOnlyList<AudioDevice> GetInputs() => Enumerate(DataFlow.Capture, false);
    public IReadOnlyList<AudioDevice> GetCableOutputs() => Enumerate(DataFlow.Render, true);
    public IReadOnlyList<AudioDevice> GetMonitorOutputs() => Enumerate(DataFlow.Render, false)
        .Where(device => !IsCable(device.Name)).ToArray();

    public string? GetDefaultPlaybackId() => GetDefaultId(DataFlow.Render);
    public string? GetDefaultCaptureId() => GetDefaultId(DataFlow.Capture);

    private static string? GetDefaultId(DataFlow direction)
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(direction, Role.Multimedia);
            return device.ID;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    public static bool IsCable(string name) => name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)
        && name.Contains("Cable", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<AudioDevice> Enumerate(DataFlow direction, bool onlyCable)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<AudioDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(direction, DeviceState.Active))
        {
            using (device)
            {
                if (!onlyCable || IsCable(device.FriendlyName))
                    devices.Add(new AudioDevice(device.ID, device.FriendlyName));
            }
        }
        return devices.OrderBy(device => device.Name.Replace(" ", "").Contains("16ch", StringComparison.OrdinalIgnoreCase))
            .ThenBy(device => device.Name).ToArray();
    }
}
