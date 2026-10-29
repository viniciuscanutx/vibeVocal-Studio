using System.IO;
using System.Text.Json;
using VibeVocalStudio.Audio;
using VibeVocalStudio.Models;

public static class MonitorChecks
{
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

        var service = new AudioDeviceService();
        var inputs = service.GetInputs().Where(device => !AudioDeviceService.IsCable(device.Name)).ToArray();
        var input = inputs.FirstOrDefault(device => device.Id == service.GetDefaultCaptureId()) ?? inputs.First();
        var output = service.GetCableOutputs().First();
        var monitorDevices = service.GetMonitorOutputs();
        var monitor = monitorDevices.FirstOrDefault(device => device.Id == service.GetDefaultPlaybackId()) ?? monitorDevices.First();
        using var engine = new AudioEngine();
        string? error = null;
        string? monitorError = null;
        engine.Failed += message => error = message;
        engine.MonitorFailed += message => monitorError = message;
        Check("Lista de retorno exclui o VB-CABLE", monitorDevices.All(device => !AudioDeviceService.IsCable(device.Name)));
        try
        {
            engine.Start(input.Id, output.Id, 40, new AudioSettings { Muted = true });
            Thread.Sleep(150);
            Check("Retorno começa desligado", !engine.IsMonitoring);
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var capturedBefore = engine.Metrics.CapturedBytes;
                engine.StartMonitoring(monitor.Id);
                Thread.Sleep(350);
                Check($"Retorno recebe amostras e cabo continua ativo, ciclo {iteration + 1}",
                    engine.IsMonitoring && engine.MonitorWrittenSamples > 0 && engine.IsRunning && engine.Metrics.CapturedBytes > capturedBefore && error is null && monitorError is null);
                Check($"Retorno respeita mudo, ciclo {iteration + 1}", engine.Metrics.OutputPeak == 0);
                engine.StopMonitoring();
                Check($"Desligar retorno preserva envio ao cabo, ciclo {iteration + 1}", !engine.IsMonitoring && engine.IsRunning);
            }
            try
            {
                engine.StartMonitoring(output.Id);
                Check("Retorno rejeita VB-CABLE", false);
            }
            catch (InvalidOperationException)
            {
                Check("Retorno rejeita VB-CABLE sem parar processamento", !engine.IsMonitoring && engine.IsRunning);
            }
            engine.StartMonitoring(monitor.Id);
            engine.Stop();
            Check("Parar processamento encerra retorno", !engine.IsMonitoring && !engine.IsRunning);
        }
        catch (Exception exception)
        {
            failures.Add(exception.ToString());
            Console.WriteLine(exception);
        }
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/monitor-validation.json", JsonSerializer.Serialize(new { Monitor = monitor, Checks = checks, Failures = failures, MainError = error, MonitorError = monitorError }, new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }
}
