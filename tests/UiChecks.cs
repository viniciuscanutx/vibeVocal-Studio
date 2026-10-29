using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using VibeVocalStudio;
using VibeVocalStudio.ViewModels;
using VibeVocalStudio.Services;

public static class UiChecks
{
    public static int Run()
    {
        var failures = new List<string>();
        var checks = new List<string>();
        var thread = new Thread(() =>
        {
            var bindingErrors = new StringWriter();
            var listener = new TextWriterTraceListener(bindingErrors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var application = new App();
            application.InitializeComponent();
            var settingsFolder = Path.Combine(Path.GetTempPath(), "VibeVocal-ui-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(new StudioViewModel(new SettingsStore(settingsFolder))) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false, Width = 980, Height = 720 };
            window.Show();
            window.Dispatcher.InvokeAsync(async () =>
            {
                void Check(string name, bool condition)
                {
                    checks.Add(name);
                    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
                    if (!condition) failures.Add(name);
                }
                try
                {
                    var model = (StudioViewModel)window.DataContext;
                    model.ResetCommand.Execute(null);
                    Check("Escuta inicia desligada", !model.MonitorEnabled);
                    Check("Supressão de ruído inicia ativada", model.NoiseSuppressionEnabled);
                    model.SelectedOutput = model.Outputs.First(device => device.Name.StartsWith("CABLE Input", StringComparison.OrdinalIgnoreCase));
                    window.UpdateLayout();
                    var gainSlider = Descendants(window).OfType<Slider>().Single(slider => AutomationProperties.GetName(slider) == "Ganho digital de entrada em decibéis");
                    gainSlider.SetCurrentValue(Slider.ValueProperty, 6d);
                    gainSlider.GetBindingExpression(Slider.ValueProperty)!.UpdateSource();
                    Check("Slider principal atualiza ganho no modelo", model.InputGainDb == 6);
                    var equalizer = Descendants(window).OfType<Slider>().Single(slider => AutomationProperties.GetName(slider) == "Presença");
                    equalizer.SetCurrentValue(Slider.ValueProperty, 4d);
                    equalizer.GetBindingExpression(Slider.ValueProperty)!.UpdateSource();
                    Check("Slider reutilizável atualiza equalização", model.PresenceDb == 4);
                    model.StartStopCommand.Execute(null);
                    await Task.Delay(700);
                    Check("Janela inicia áudio e bloqueia troca de dispositivos", model.IsRunning && !model.CanSelectDevices);
                    var noiseToggle = Descendants(window).OfType<CheckBox>().Single(control => AutomationProperties.GetName(control) == "Suprimir ruído durante a fala");
                    noiseToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                    noiseToggle.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                    await Task.Delay(150);
                    Check("Supressão pode ser desligada durante a captura", !model.NoiseSuppressionEnabled && model.IsRunning);
                    noiseToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                    noiseToggle.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                    await Task.Delay(150);
                    Check("Supressão pode ser religada durante a captura", model.NoiseSuppressionEnabled && model.IsRunning);
                    model.Muted = true;
                    await Task.Delay(200);
                    Check("Janela apresenta estado de mudo", model.SignalStatus == "Silenciado");
                    var monitorToggle = Descendants(window).OfType<CheckBox>().Single(control => AutomationProperties.GetName(control) == "Ouvir meu microfone");
                    monitorToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                    monitorToggle.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                    await Task.Delay(250);
                    Check("Controle de escuta ativa retorno durante processamento", model.MonitorEnabled && model.IsRunning && !model.CanSelectMonitor && model.MonitorStatus.StartsWith("Retorno ligado"));
                    monitorToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                    monitorToggle.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                    Check("Desligar escuta mantém processamento", !model.MonitorEnabled && model.IsRunning && model.CanSelectMonitor);
                    model.StartStopCommand.Execute(null);
                    Check("Janela para áudio e libera dispositivos", !model.IsRunning && model.CanSelectDevices);
                    model.ResetCommand.Execute(null);
                    model.Muted = false;
                    Check("Restaurar ajustes atualiza controles", model.InputGainDb == 0 && model.PresenceDb == 0 && gainSlider.Value == 0);
                    listener.Flush();
                    Check("Interface sem erros de binding WPF", string.IsNullOrWhiteSpace(bindingErrors.ToString()));
                }
                catch (Exception exception)
                {
                    failures.Add(exception.ToString());
                    Console.WriteLine(exception);
                }
                finally
                {
                    Directory.CreateDirectory("artifacts");
                    File.WriteAllText("artifacts/ui-validation.json", JsonSerializer.Serialize(new { Checks = checks, Failures = failures, BindingErrors = bindingErrors.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
                    window.Close();
                    File.Delete(Path.Combine(settingsFolder, "settings.json"));
                    Directory.Delete(settingsFolder);
                    application.Shutdown();
                }
            }, DispatcherPriority.ApplicationIdle);
            application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failures.Count == 0 ? 0 : 1;
    }

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
