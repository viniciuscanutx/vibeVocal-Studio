using System.ComponentModel;
using System.Drawing;
using System.Windows;
using VibeVocalStudio.ViewModels;
using Forms = System.Windows.Forms;

namespace VibeVocalStudio.Services;

public sealed class TrayController : IDisposable
{
    private readonly Window window;
    private readonly StudioViewModel model;
    private readonly Action exitApplication;
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu;
    private readonly Forms.ToolStripMenuItem status;
    private readonly Icon trayIcon;
    private WindowState restoreState = WindowState.Normal;
    private bool exiting;
    private bool disposed;

    public TrayController(Window window, StudioViewModel model, Action exitApplication)
    {
        this.window = window;
        this.model = model;
        this.exitApplication = exitApplication;
        using var iconStream = typeof(TrayController).Assembly.GetManifestResourceStream("VibeVocalStudio.Assets.Microphone.ico")
            ?? throw new InvalidOperationException("Ícone da bandeja não encontrado.");
        trayIcon = new Icon(iconStream);
        menu = new Forms.ContextMenuStrip();
        status = new Forms.ToolStripMenuItem { Enabled = false };
        menu.Items.Add(status);
        menu.Items.Add("Abrir vibeVocal Studio", null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Exit());
        icon = new Forms.NotifyIcon { Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => ShowWindow();
        icon.BalloonTipClicked += (_, _) => ShowWindow();
        window.Closing += OnClosing;
        window.StateChanged += OnStateChanged;
        model.PropertyChanged += OnPropertyChanged;
        model.BackgroundError += ShowError;
        UpdateStatus();
    }

    public void ShowWindow()
    {
        if (disposed || exiting)
            return;
        window.Show();
        window.WindowState = restoreState;
        window.Activate();
    }

    public void HideWindow() => window.Hide();

    public void AllowClose() => exiting = true;

    public void Exit()
    {
        if (exiting)
            return;
        exiting = true;
        window.Close();
        exitApplication();
    }

    public void ShowError(string message)
    {
        if (!disposed)
            icon.ShowBalloonTip(5000, "vibeVocal Studio — áudio interrompido", message, Forms.ToolTipIcon.Warning);
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (exiting)
            return;
        args.Cancel = true;
        HideWindow();
    }

    private void OnStateChanged(object? sender, EventArgs args)
    {
        if (window.WindowState == WindowState.Minimized)
            HideWindow();
        else
            restoreState = window.WindowState;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(StudioViewModel.IsRunning) or nameof(StudioViewModel.Status) or nameof(StudioViewModel.Muted) or "" or null)
            UpdateStatus();
    }

    private void UpdateStatus()
    {
        var description = model.IsRunning ? model.Muted ? "Microfone silenciado" : "Áudio ativo no VB-CABLE" : model.Status;
        status.Text = description;
        var tooltip = "vibeVocal Studio — " + description;
        icon.Text = tooltip[..Math.Min(63, tooltip.Length)];
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        window.Closing -= OnClosing;
        window.StateChanged -= OnStateChanged;
        model.PropertyChanged -= OnPropertyChanged;
        model.BackgroundError -= ShowError;
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
        trayIcon.Dispose();
    }
}
