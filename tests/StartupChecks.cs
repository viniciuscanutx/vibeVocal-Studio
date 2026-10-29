using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VibeVocalStudio;
using VibeVocalStudio.Models;
using VibeVocalStudio.Services;
using VibeVocalStudio.ViewModels;

public static class StartupChecks
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

        using (var activated = new ManualResetEventSlim())
        {
            var identity = "VibeVocal-test-" + Guid.NewGuid().ToString("N");
            using var primary = new SingleInstanceService(identity, () => activated.Set(), false);
            using (var secondary = new SingleInstanceService(identity, () => { }, true))
                Check("Segunda instância sinaliza a primeira sem assumir o áudio", primary.IsPrimary && !secondary.IsPrimary && activated.Wait(1000));
            activated.Reset();
            using var startupDuplicate = new SingleInstanceService(identity, () => { }, false);
            Check("Inicialização repetida do Windows não abre a janela", !startupDuplicate.IsPrimary && !activated.Wait(100));
        }

        var registration = new StartupRegistration(@"C:\Pasta com espaços\vibeVocal Studio.exe");
        Check("Comando de inicialização protege caminhos com espaços", registration.Command == "\"C:\\Pasta com espaços\\vibeVocal Studio.exe\" --startup");
        var thread = new Thread(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "VibeVocal-startup-" + Guid.NewGuid().ToString("N"));
            var store = new SettingsStore(folder);
            store.Save(new StudioSettings { Audio = new AudioSettings { InputGainDb = 13, OutputGainDb = 6 }, BufferMilliseconds = 80 });
            var fakeRegistration = new FakeStartupRegistration();
            var application = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.InitializeComponent();
            var model = new StudioViewModel(store, fakeRegistration);
            var window = new MainWindow(model) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false, Width = 980, Height = 720 };
            var exitRequested = false;
            TrayController? tray = null;
            window.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    tray = new TrayController(window, model, () => exitRequested = true);
                    await model.StartAutomaticallyAsync();
                    Check("Inicialização automática desativada mantém áudio parado", !model.IsRunning);
                    model.AutoStartAudio = true;
                    model.StartInTray = true;
                    model.Muted = true;
                    Check("Janela pode existir diretamente na bandeja", !window.IsVisible && !model.MonitorEnabled);
                    await model.StartAutomaticallyAsync();
                    await Task.Delay(250);
                    Check("Áudio inicia automaticamente com janela oculta", model.IsRunning && !window.IsVisible && !model.MonitorEnabled);
                    Check("Inicialização preserva ganhos e buffer", model.InputGainDb == 13 && model.OutputGainDb == 6 && model.SelectedBuffer.Milliseconds == 80);
                    await model.StartAutomaticallyAsync();
                    Check("Inicialização repetida não para áudio já ativo", model.IsRunning);
                    tray.ShowWindow();
                    window.UpdateLayout();
                    Check("Abrir pelo ícone restaura a janela", window.IsVisible && window.WindowState == WindowState.Normal);
                    window.Close();
                    Check("Fechar pelo X mantém janela oculta e áudio ativo", !window.IsVisible && model.IsRunning && !exitRequested);
                    tray.ShowWindow();
                    window.WindowState = WindowState.Minimized;
                    Check("Minimizar envia para a bandeja sem parar áudio", !window.IsVisible && model.IsRunning);
                    tray.ShowWindow();
                    Check("Restaurar após minimizar volta ao estado normal", window.IsVisible && window.WindowState == WindowState.Normal);
                    var startupToggle = UiChecks.Descendants(window).OfType<CheckBox>().Single(control => AutomationProperties.GetName(control) == "Abrir com o Windows");
                    SetChecked(startupToggle, true);
                    Check("Controle de iniciar com Windows registra a preferência", model.StartWithWindows && fakeRegistration.IsEnabled);
                    SetChecked(startupToggle, false);
                    Check("Controle permite remover inicialização com Windows", !model.StartWithWindows && !fakeRegistration.IsEnabled);
                    fakeRegistration.FailWrites = true;
                    SetChecked(startupToggle, true);
                    await Task.Delay(50);
                    Check("Falha no registro mantém opção desativada e explica o erro", !model.StartWithWindows && startupToggle.IsChecked == false && model.StartupStatus.Contains("Não foi possível alterar"));
                    foreach (var name in new[] { "Iniciar áudio automaticamente", "Abrir direto na bandeja" })
                    {
                        var toggle = UiChecks.Descendants(window).OfType<CheckBox>().Single(control => AutomationProperties.GetName(control) == name);
                        SetChecked(toggle, false);
                        Check($"Controle '{name}' atualiza preferência", name == "Iniciar áudio automaticamente" ? !model.AutoStartAudio : !model.StartInTray);
                        SetChecked(toggle, true);
                    }
                    model.StartStopCommand.Execute(null);
                    Check("Parar continua disponível após restauração da bandeja", !model.IsRunning);
                    using (var canceled = new CancellationTokenSource())
                    {
                        canceled.Cancel();
                        await model.StartAutomaticallyAsync(canceled.Token);
                        Check("Inicialização cancelada não abre a captura", !model.IsRunning);
                    }
                    await model.StartAutomaticallyAsync();
                    Check("Processamento pode iniciar novamente", model.IsRunning);
                    tray.Exit();
                    Check("Sair encerra janela e captura", exitRequested && !window.IsVisible && !model.IsRunning);
                    var saved = store.Load();
                    Check("Preferências de inicialização persistem sem alterar áudio", saved.AutoStartAudio && saved.StartInTray && saved.Audio.InputGainDb == 13 && saved.Audio.OutputGainDb == 6);
                    using var closingModel = new StudioViewModel(store, fakeRegistration);
                    var closingWindow = new MainWindow(closingModel);
                    using var closingTray = new TrayController(closingWindow, closingModel, () => { });
                    closingTray.AllowClose();
                    closingWindow.Close();
                    Check("Encerramento da sessão pode fechar a janela sem recolher novamente", !closingWindow.IsLoaded && !closingModel.IsRunning);
                }
                catch (Exception exception)
                {
                    failures.Add(exception.ToString());
                    Console.WriteLine(exception);
                }
                finally
                {
                    tray?.AllowClose();
                    tray?.Dispose();
                    window.Close();
                    model.Dispose();
                    File.Delete(Path.Combine(folder, "settings.json"));
                    Directory.Delete(folder);
                    application.Shutdown();
                }
            }, DispatcherPriority.ApplicationIdle);
            application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/startup-validation.json", JsonSerializer.Serialize(new { Checks = checks, Failures = failures }, new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }

    private static void SetChecked(CheckBox control, bool value)
    {
        control.SetCurrentValue(ToggleButton.IsCheckedProperty, value);
        control.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
    }

    private sealed class FakeStartupRegistration : IStartupRegistration
    {
        public bool IsEnabled { get; private set; }
        public bool FailWrites { get; set; }
        public void SetEnabled(bool enabled)
        {
            if (FailWrites)
                throw new UnauthorizedAccessException("Falha simulada no registro.");
            IsEnabled = enabled;
        }
    }
}
