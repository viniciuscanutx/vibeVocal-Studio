using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VibeVocalStudio.Audio;
using VibeVocalStudio.Services;
using VibeVocalStudio.ViewModels;

namespace VibeVocalStudio;

public partial class App : Application
{
    private SingleInstanceService? instance;
    private TrayController? tray;
    private StudioViewModel? model;
    private readonly CancellationTokenSource shutdown = new();

    protected override void OnStartup(StartupEventArgs args)
    {
        base.OnStartup(args);
        if (MainWindow is not null)
            return;
        if (args.Args.Length == 2 && args.Args[0] == "--diagnostics")
        {
            var devices = new AudioDeviceService();
            var report = new { Inputs = devices.GetInputs(), Outputs = devices.GetCableOutputs() };
            File.WriteAllText(args.Args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown();
            return;
        }
        if (args.Args.Length == 2 && args.Args[0] == "--snapshot")
        {
            var window = new MainWindow();
            MainWindow = window;
            window.ShowActivated = false;
            window.Left = -20000;
            window.Top = -20000;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Height = 1400;
            window.Show();
            Dispatcher.InvokeAsync(() =>
            {
                window.UpdateLayout();
                var content = (FrameworkElement)window.Content;
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(args.Args[1])) encoder.Save(stream);
                window.Close();
            }, DispatcherPriority.ApplicationIdle);
            return;
        }
        instance = new SingleInstanceService(SettingsStore.DefaultDirectory, () =>
        {
            if (!Dispatcher.HasShutdownStarted)
                Dispatcher.BeginInvoke(ShowMainWindow);
        }, !args.Args.Contains("--startup"));
        if (!instance.IsPrimary)
        {
            Shutdown();
            return;
        }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        model = new StudioViewModel();
        var mainWindow = new MainWindow(model);
        MainWindow = mainWindow;
        try
        {
            tray = new TrayController(mainWindow, model, Shutdown);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MessageBox.Show($"Não foi possível criar o ícone da bandeja. A janela continuará aberta. {exception.Message}", "vibeVocal Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        if (!model.StartInTray || tray is null)
            mainWindow.Show();
        Dispatcher.InvokeAsync(async () => await model.StartAutomaticallyAsync(shutdown.Token), DispatcherPriority.ApplicationIdle);
    }

    private void ShowMainWindow()
    {
        if (tray is not null)
            tray.ShowWindow();
        else if (MainWindow is not null)
        {
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs args)
    {
        shutdown.Cancel();
        tray?.AllowClose();
        base.OnSessionEnding(args);
    }

    protected override void OnExit(ExitEventArgs args)
    {
        shutdown.Cancel();
        tray?.Dispose();
        model?.Dispose();
        instance?.Dispose();
        shutdown.Dispose();
        base.OnExit(args);
    }
}
