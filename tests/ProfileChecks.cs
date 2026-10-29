using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VibeVocalStudio;
using VibeVocalStudio.Models;
using VibeVocalStudio.Services;
using VibeVocalStudio.ViewModels;

public static class ProfileChecks
{
    public static int Run()
    {
        var failures = new List<string>();
        var checks = new List<string>();
        var directory = Path.GetFullPath(Path.Combine("artifacts", "profile-tests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        void Check(string name, bool condition)
        {
            checks.Add(name);
            Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
            if (!condition) failures.Add(name);
        }

        var olderDirectory = Path.Combine(directory, "dist", "Data");
        var newerDirectory = Path.Combine(directory, "bin", "Data");
        new SettingsStore(olderDirectory).Save(new StudioSettings { Audio = new AudioSettings { InputGainDb = 13 } });
        new SettingsStore(newerDirectory).Save(new StudioSettings { Audio = new AudioSettings { InputGainDb = 11 } });
        var olderPath = Path.Combine(olderDirectory, "settings.json");
        var newerPath = Path.Combine(newerDirectory, "settings.json");
        File.SetLastWriteTimeUtc(olderPath, DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(newerPath, DateTime.UtcNow.AddMinutes(-1));
        var sharedDirectory = Path.Combine(directory, "shared");
        var shared = new SettingsStore(sharedDirectory, [olderPath, newerPath]);
        Check("Migração escolhe a cópia mais recente entre bin e dist", shared.Load().Audio.InputGainDb == 11);
        Check("Migração preserva os arquivos anteriores", File.Exists(olderPath) && File.Exists(newerPath));
        Check("Outra cópia lê os mesmos ajustes compartilhados", new SettingsStore(sharedDirectory).Load().Audio.InputGainDb == 11);
        shared.Save(new StudioSettings { Audio = new AudioSettings { InputGainDb = 8 } });
        File.SetLastWriteTimeUtc(olderPath, DateTime.UtcNow.AddMinutes(1));
        Check("Arquivo antigo não sobrescreve ajustes já migrados", shared.Load().Audio.InputGainDb == 8);
        File.WriteAllText(newerPath, "inválido");
        File.SetLastWriteTimeUtc(newerPath, DateTime.UtcNow.AddMinutes(2));
        Check("Migração ignora arquivo inválido e recupera outra cópia", new SettingsStore(Path.Combine(directory, "fallback"), [olderPath, newerPath]).Load().Audio.InputGainDb == 13);

        var thread = new Thread(() =>
        {
            var bindingErrors = new StringWriter();
            var listener = new TextWriterTraceListener(bindingErrors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var application = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.InitializeComponent();
            var store = new SettingsStore(Path.Combine(directory, "interface"));
            var expected = new AudioSettings
            {
                InputGainDb = 11.5,
                OutputGainDb = 4.5,
                BassDb = -4,
                PresenceDb = 7,
                TrebleDb = 3,
                GateThresholdDb = -54,
                CompressorThresholdDb = -9,
                CompressorRatio = 5,
                HighPassEnabled = false,
                NoiseSuppressionEnabled = false,
                GateEnabled = true,
                CompressorEnabled = true
            };
            store.Save(new StudioSettings { Audio = expected, BufferMilliseconds = 80 });
            MainWindow? window = null;
            StudioViewModel? model = null;
            try
            {
                model = new StudioViewModel(store, new FakeRegistration());
                Check("Modelo recupera todos os ajustes antes de abrir a janela", ReadAudio(model) == expected);
                window = CreateWindow(model);
                Pump(100);
                Check("Abrir a interface preserva todos os ajustes carregados", ReadAudio(model) == expected);
                Check("Reaplicar fica indisponível antes do primeiro perfil", !model.ApplyProfileCommand.CanExecute(null));
                var saveButton = UiChecks.Descendants(window).OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Salvar perfil de voz");
                saveButton.Command.Execute(null);
                Check("Botão salva todos os ajustes do perfil", store.LoadProfile()?.Audio == expected);
                Check("Reaplicar fica disponível após salvar", model.ApplyProfileCommand.CanExecute(null));
                model.InputGainDb = -4;
                model.BassDb = 10;
                model.GateThresholdDb = -62;
                model.NoiseSuppressionEnabled = true;
                Pump(650);
                Check("Salvamento automático persiste sem fechar", store.Load().Audio.InputGainDb == -4 && store.Load().Audio.GateThresholdDb == -62);
                Check("Salvamento automático não substitui o perfil", store.LoadProfile()?.Audio == expected);
                var inputId = model.SelectedInput?.Id;
                var outputId = model.SelectedOutput?.Id;
                model.Muted = true;
                model.Bypass = true;
                var applyButton = UiChecks.Descendants(window).OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Reaplicar perfil de voz");
                applyButton.Command.Execute(null);
                Pump(100);
                Check("Botão reaplica todos os ajustes de voz", ReadAudio(model) with { Muted = false, Bypass = false } == expected);
                Check("Reaplicar preserva mudo e comparação temporários", model.Muted && model.Bypass);
                Check("Perfil mantém dispositivos e buffer selecionados", model.SelectedInput?.Id == inputId && model.SelectedOutput?.Id == outputId && model.SelectedBuffer.Milliseconds == 80);
                model.ResetCommand.Execute(null);
                Check("Restaurar padrões não apaga o perfil salvo", store.LoadProfile()?.Audio == expected);
                model.ApplyProfileCommand.Execute(null);
                window.Close();
                model.Dispose();
                model = new StudioViewModel(store, new FakeRegistration());
                window = CreateWindow(model);
                Pump(100);
                Check("Fechar e reabrir mantém todos os ajustes e o perfil", ReadAudio(model) == expected && model.ApplyProfileCommand.CanExecute(null));
                Check("Reabrir mantém monitoramento e captura desligados", !model.MonitorEnabled && !model.IsRunning);
                model.InputGainDb = 9;
                window.Close();
                model.Dispose();
                Check("Fechar antes do prazo salva a última alteração", store.Load().Audio.InputGainDb == 9);
                model = new StudioViewModel(store, new FakeRegistration());
                window = CreateWindow(model);
                Pump(100);
                File.WriteAllText(Path.Combine(directory, "interface", "voice-profile.json"), "perfil inválido");
                model.ApplyProfileCommand.Execute(null);
                Check("Perfil inválido não apaga ajustes atuais", model.InputGainDb == 9 && model.ProfileStatus.Contains("Não foi possível"));
                model.SaveProfileCommand.Execute(null);
                Check("Salvar substitui perfil inválido pelos ajustes atuais", store.LoadProfile()?.Audio.InputGainDb == 9);
                ((ScrollViewer)window.Content).ScrollToEnd();
                Pump(50);
                window.UpdateLayout();
                var content = (FrameworkElement)window.Content;
                var image = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(Path.Combine(directory, "interface-profile.png"))) encoder.Save(stream);
                listener.Flush();
                Check("Interface de perfis sem erros de binding", string.IsNullOrWhiteSpace(bindingErrors.ToString()));
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                Console.WriteLine(exception);
            }
            finally
            {
                window?.Close();
                model?.Dispose();
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                listener.Dispose();
                application.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { Checks = checks, Failures = failures }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Resultado: {checks.Count} verificações, {failures.Count} falhas. Artefatos: {directory}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static MainWindow CreateWindow(StudioViewModel model)
    {
        var window = new MainWindow(model) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false, Width = 1180, Height = 1600 };
        window.Show();
        return window;
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static AudioSettings ReadAudio(StudioViewModel model) => new()
    {
        InputGainDb = model.InputGainDb,
        OutputGainDb = model.OutputGainDb,
        BassDb = model.BassDb,
        PresenceDb = model.PresenceDb,
        TrebleDb = model.TrebleDb,
        HighPassEnabled = model.HighPassEnabled,
        NoiseSuppressionEnabled = model.NoiseSuppressionEnabled,
        GateEnabled = model.GateEnabled,
        GateThresholdDb = model.GateThresholdDb,
        CompressorEnabled = model.CompressorEnabled,
        CompressorThresholdDb = model.CompressorThresholdDb,
        CompressorRatio = model.CompressorRatio,
        Muted = model.Muted,
        Bypass = model.Bypass
    };

    private sealed class FakeRegistration : IStartupRegistration
    {
        public bool IsEnabled => false;
        public void SetEnabled(bool enabled) => throw new InvalidOperationException("Este teste não modifica a inicialização do Windows.");
    }
}
