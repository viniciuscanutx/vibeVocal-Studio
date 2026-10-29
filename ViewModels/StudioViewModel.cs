using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using VibeVocalStudio.Audio;
using VibeVocalStudio.Models;
using VibeVocalStudio.Services;

namespace VibeVocalStudio.ViewModels;

public sealed class StudioViewModel : ObservableObject, IDisposable
{
    private readonly AudioEngine engine = new();
    private readonly AudioDeviceService devices = new();
    private readonly SettingsStore store;
    private readonly IStartupRegistration startupRegistration;
    private readonly string? startupInputId;
    private readonly string? startupOutputId;
    private readonly string? startupMonitorId;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer meterTimer;
    private readonly DispatcherTimer saveTimer;
    private AudioSettings audio = new();
    private AudioDevice? selectedInput;
    private AudioDevice? selectedOutput;
    private AudioDevice? selectedMonitor;
    private bool monitorEnabled;
    private string monitorStatus = "Use fones para evitar eco e microfonia.";
    private BufferOption selectedBuffer;
    private bool isRunning;
    private bool refreshing;
    private string status = "Pronto para iniciar";
    private string detail = "Escolha o microfone e inicie o processamento.";
    private double inputDb = -60;
    private double outputDb = -60;
    private string processingTime = "—";
    private string queuedAudio = "—";
    private string reduction = "0 dB";
    private string signalStatus = "Aguardando áudio";
    private string format = "Captação desligada";
    private string savedStatus = "Ajustes salvos automaticamente";
    private string profileStatus = "Salve seus ajustes de voz para poder recuperá-los depois.";
    private bool hasSavedProfile;
    private DateTime clippedUntil;
    private double maxProcessingTime;
    private bool disposed;
    private bool startWithWindows;
    private bool autoStartAudio;
    private bool startInTray;
    private bool automaticallyStarting;
    private CancellationTokenSource? automaticStart;
    private string startupStatus = "Fechar ou minimizar mantém o áudio na bandeja. Para encerrar, clique com o botão direito no ícone e escolha Sair.";

    public StudioViewModel(SettingsStore? settingsStore = null, IStartupRegistration? registration = null)
    {
        dispatcher = Dispatcher.CurrentDispatcher;
        store = settingsStore ?? new SettingsStore();
        startupRegistration = registration ?? new StartupRegistration();
        Buffers = [new(20, "20 ms · Menor atraso"), new(40, "40 ms · Equilibrado"), new(80, "80 ms · Mais estabilidade")];
        selectedBuffer = Buffers[1];
        StartStopCommand = new RelayCommand(ToggleEngine, () => IsRunning || SelectedInput is not null && SelectedOutput is not null);
        RefreshCommand = new RelayCommand(RefreshDevices, () => !IsRunning);
        ResetCommand = new RelayCommand(ResetAudio);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        ApplyProfileCommand = new RelayCommand(ApplyProfile, () => hasSavedProfile);
        meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, (_, _) => UpdateMeters(), dispatcher);
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        saveTimer.Tick += (_, _) => Save();
        engine.Failed += message => dispatcher.BeginInvoke(() => HandleFailure(message));
        engine.MonitorFailed += message => dispatcher.BeginInvoke(() => HandleMonitorFailure(message));
        StudioSettings saved;
        try
        {
            saved = store.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            saved = new StudioSettings();
            savedStatus = "Não foi possível carregar os ajustes. Usando valores iniciais.";
        }
        audio = saved.Audio;
        autoStartAudio = saved.AutoStartAudio;
        startInTray = saved.StartInTray;
        startupInputId = saved.InputDeviceId;
        startupOutputId = saved.OutputDeviceId;
        startupMonitorId = saved.MonitorDeviceId;
        try
        {
            startWithWindows = startupRegistration.IsEnabled;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            startupStatus = $"Não foi possível consultar a inicialização com o Windows. {exception.Message}";
        }
        selectedBuffer = Buffers.First(option => option.Milliseconds == saved.BufferMilliseconds);
        RefreshDevices(saved.InputDeviceId, saved.OutputDeviceId, saved.MonitorDeviceId);
        ReadProfileStatus();
    }

    public ObservableCollection<AudioDevice> Inputs { get; } = [];
    public ObservableCollection<AudioDevice> Outputs { get; } = [];
    public ObservableCollection<AudioDevice> MonitorOutputs { get; } = [];
    public IReadOnlyList<BufferOption> Buffers { get; }
    public RelayCommand StartStopCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand SaveProfileCommand { get; }
    public RelayCommand ApplyProfileCommand { get; }
    public string ProfileStatus { get => profileStatus; private set => Set(ref profileStatus, value); }
    public event Action<string>? BackgroundError;
    public string StartupStatus { get => startupStatus; private set => Set(ref startupStatus, value); }
    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (startWithWindows == value)
                return;
            try
            {
                startupRegistration.SetEnabled(value);
                Set(ref startWithWindows, value);
                StartupStatus = value ? "O vibeVocal Studio abrirá quando você entrar no Windows. Mantenha o aplicativo nesta pasta."
                    : "Inicialização com o Windows desativada. Você pode continuar abrindo pelo atalho.";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
            {
                StartupStatus = $"Não foi possível alterar a inicialização com o Windows. {exception.Message}";
                Notify(nameof(StartWithWindows));
            }
        }
    }
    public bool AutoStartAudio
    {
        get => autoStartAudio;
        set
        {
            if (!Set(ref autoStartAudio, value))
                return;
            if (!value)
                automaticStart?.Cancel();
            ScheduleSave();
        }
    }
    public bool StartInTray
    {
        get => startInTray;
        set { if (Set(ref startInTray, value)) ScheduleSave(); }
    }
    public bool IsRunning { get => isRunning; private set { Set(ref isRunning, value); Notify(nameof(CanSelectDevices)); Notify(nameof(StartStopText)); RefreshCommands(); } }
    public bool CanSelectDevices => !IsRunning;
    public string StartStopText => IsRunning ? "Parar" : "Iniciar";
    public string Status { get => status; private set => Set(ref status, value); }
    public string Detail { get => detail; private set => Set(ref detail, value); }
    public string Format { get => format; private set => Set(ref format, value); }
    public string SavedStatus { get => savedStatus; private set => Set(ref savedStatus, value); }
    public string ProcessingTime { get => processingTime; private set => Set(ref processingTime, value); }
    public string QueuedAudio { get => queuedAudio; private set => Set(ref queuedAudio, value); }
    public string Reduction { get => reduction; private set => Set(ref reduction, value); }
    public string SignalStatus { get => signalStatus; private set => Set(ref signalStatus, value); }
    public string MonitorStatus { get => monitorStatus; private set => Set(ref monitorStatus, value); }
    public bool CanSelectMonitor => !MonitorEnabled;
    public bool HasMonitorDevice => SelectedMonitor is not null;
    public bool MonitorEnabled
    {
        get => monitorEnabled;
        set
        {
            if (!Set(ref monitorEnabled, value))
                return;
            Notify(nameof(CanSelectMonitor));
            ApplyMonitoring();
        }
    }
    public AudioDevice? SelectedMonitor
    {
        get => selectedMonitor;
        set
        {
            if (!Set(ref selectedMonitor, value))
                return;
            Notify(nameof(HasMonitorDevice));
            if (!refreshing)
                ApplyMonitoring();
            ScheduleSave();
        }
    }
    public double InputMeter { get => inputDb; private set { Set(ref inputDb, value); Notify(nameof(InputLevel)); } }
    public double OutputMeter { get => outputDb; private set { Set(ref outputDb, value); Notify(nameof(OutputLevel)); } }
    public string InputLevel => InputMeter <= -60 ? "−∞ dBFS" : $"{InputMeter:0.0} dBFS";
    public string OutputLevel => OutputMeter <= -60 ? "−∞ dBFS" : $"{OutputMeter:0.0} dBFS";

    public AudioDevice? SelectedInput
    {
        get => selectedInput;
        set { if (Set(ref selectedInput, value)) { RefreshCommands(); ScheduleSave(); } }
    }
    public AudioDevice? SelectedOutput
    {
        get => selectedOutput;
        set { if (Set(ref selectedOutput, value)) { RefreshCommands(); ScheduleSave(); } }
    }
    public BufferOption SelectedBuffer
    {
        get => selectedBuffer;
        set { if (Set(ref selectedBuffer, value)) ScheduleSave(); }
    }

    public double InputGainDb { get => audio.InputGainDb; set => Update(audio with { InputGainDb = value }, nameof(InputGainDb)); }
    public double OutputGainDb { get => audio.OutputGainDb; set => Update(audio with { OutputGainDb = value }, nameof(OutputGainDb)); }
    public double BassDb { get => audio.BassDb; set => Update(audio with { BassDb = value }, nameof(BassDb)); }
    public double PresenceDb { get => audio.PresenceDb; set => Update(audio with { PresenceDb = value }, nameof(PresenceDb)); }
    public double TrebleDb { get => audio.TrebleDb; set => Update(audio with { TrebleDb = value }, nameof(TrebleDb)); }
    public double GateThresholdDb { get => audio.GateThresholdDb; set => Update(audio with { GateThresholdDb = value }, nameof(GateThresholdDb)); }
    public double CompressorThresholdDb { get => audio.CompressorThresholdDb; set => Update(audio with { CompressorThresholdDb = value }, nameof(CompressorThresholdDb)); }
    public double CompressorRatio { get => audio.CompressorRatio; set => Update(audio with { CompressorRatio = value }, nameof(CompressorRatio)); }
    public bool HighPassEnabled { get => audio.HighPassEnabled; set => Update(audio with { HighPassEnabled = value }, nameof(HighPassEnabled)); }
    public bool NoiseSuppressionEnabled { get => audio.NoiseSuppressionEnabled; set => Update(audio with { NoiseSuppressionEnabled = value }, nameof(NoiseSuppressionEnabled)); }
    public bool GateEnabled { get => audio.GateEnabled; set => Update(audio with { GateEnabled = value }, nameof(GateEnabled)); }
    public bool CompressorEnabled { get => audio.CompressorEnabled; set => Update(audio with { CompressorEnabled = value }, nameof(CompressorEnabled)); }
    public bool Bypass { get => audio.Bypass; set => Update(audio with { Bypass = value }, nameof(Bypass)); }
    public bool Muted { get => audio.Muted; set => Update(audio with { Muted = value }, nameof(Muted)); }
    public string MuteText => Muted ? "Microfone silenciado" : "Silenciar";

    private void Update(AudioSettings value, string property)
    {
        audio = value.Sanitize();
        engine.UpdateSettings(audio);
        Notify(property);
        Notify(nameof(MuteText));
        ScheduleSave();
    }

    private void RefreshCommands()
    {
        StartStopCommand.Refresh();
        RefreshCommand.Refresh();
    }

    private void RefreshDevices() => RefreshDevices(SelectedInput?.Id, SelectedOutput?.Id, SelectedMonitor?.Id);

    private void RefreshDevices(string? inputId, string? outputId, string? monitorId)
    {
        refreshing = true;
        try
        {
            var inputs = devices.GetInputs().Where(device => !AudioDeviceService.IsCable(device.Name)).ToArray();
            var outputs = devices.GetCableOutputs();
            var monitorOutputs = devices.GetMonitorOutputs();
            var defaultPlaybackId = devices.GetDefaultPlaybackId();
            var defaultCaptureId = devices.GetDefaultCaptureId();
            Inputs.Clear();
            Outputs.Clear();
            MonitorOutputs.Clear();
            foreach (var device in inputs) Inputs.Add(device);
            foreach (var device in outputs) Outputs.Add(device);
            foreach (var device in monitorOutputs) MonitorOutputs.Add(device);
            SelectedInput = Inputs.FirstOrDefault(device => device.Id == inputId)
                ?? Inputs.FirstOrDefault(device => device.Id == defaultCaptureId) ?? Inputs.FirstOrDefault();
            SelectedOutput = Outputs.FirstOrDefault(device => device.Id == outputId) ?? Outputs.FirstOrDefault();
            SelectedMonitor = MonitorOutputs.FirstOrDefault(device => device.Id == monitorId)
                ?? MonitorOutputs.FirstOrDefault(device => device.Id == defaultPlaybackId) ?? MonitorOutputs.FirstOrDefault();
            MonitorStatus = SelectedMonitor is null ? "Conecte um fone e atualize os dispositivos." : "Use fones para evitar eco e microfonia.";
            Status = SelectedInput is null ? "Selecione o microfone" : SelectedOutput is null ? "VB-CABLE não encontrado" : "Pronto para iniciar";
            Detail = SelectedOutput is null ? "Instale o VB-CABLE, reinicie o Windows se necessário e clique em Atualizar."
                : "No Discord, jogo ou chamada, selecione CABLE Output como microfone.";
        }
        catch (Exception exception)
        {
            Status = "Não foi possível consultar os dispositivos";
            Detail = exception.Message;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void ToggleEngine()
    {
        automaticStart?.Cancel();
        if (IsRunning)
        {
            engine.Stop();
            IsRunning = false;
            Status = "Processamento parado";
            Detail = "Clique em Iniciar para enviar sua voz ao VB-CABLE.";
            Format = "Captação desligada";
            ApplyMonitoring();
            return;
        }
        TryStartAudio();
    }

    private bool TryStartAudio()
    {
        if (SelectedInput is null || SelectedOutput is null)
            return false;
        try
        {
            engine.Start(SelectedInput.Id, SelectedOutput.Id, SelectedBuffer.Milliseconds, audio);
            IsRunning = true;
            maxProcessingTime = 0;
            Status = "Enviando áudio ao VB-CABLE";
            Detail = "No aplicativo de destino, use CABLE Output como microfone. O áudio continua ativo com a janela recolhida na bandeja.";
            Format = engine.FormatDescription;
            ApplyMonitoring();
            return true;
        }
        catch (Exception exception)
        {
            var message = exception.HResult switch
            {
                unchecked((int)0x8889000A) => "O dispositivo está ocupado. Tente a saída CABLE Input e feche programas que usam o áudio em modo exclusivo.",
                unchecked((int)0x88890004) => "O dispositivo foi desconectado. Reconecte-o e atualize a lista.",
                unchecked((int)0x80070005) => "Permita acesso ao microfone para aplicativos da área de trabalho nas configurações de privacidade do Windows.",
                _ => exception.Message
            };
            HandleFailure(message);
            return false;
        }
    }

    public async Task StartAutomaticallyAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || !AutoStartAudio || IsRunning)
            return;
        automaticStart?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        automaticStart = cancellation;
        automaticallyStarting = true;
        try
        {
            for (var attempt = 0; attempt < 15; attempt++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (!AutoStartAudio || disposed || IsRunning)
                    return;
                if (attempt > 0 || SelectedInput is null || SelectedOutput is null)
                    RefreshDevices(startupInputId, startupOutputId, startupMonitorId);
                if (TryStartAudio())
                    return;
                if (attempt < 14)
                {
                    Status = "Aguardando microfone e VB-CABLE";
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellation.Token);
                }
            }
            Status = "Não foi possível iniciar automaticamente";
            BackgroundError?.Invoke("Confira o microfone e o VB-CABLE. Abra o painel da bandeja e clique em Iniciar.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!IsRunning && !disposed)
                Status = "Inicialização automática cancelada";
        }
        finally
        {
            if (ReferenceEquals(automaticStart, cancellation))
            {
                automaticStart = null;
                automaticallyStarting = false;
            }
        }
    }

    private void HandleFailure(string message)
    {
        if (disposed)
            return;
        engine.Stop();
        IsRunning = false;
        Status = "Áudio interrompido";
        Format = "Captação desligada";
        ApplyMonitoring();
        Detail = $"Verifique a conexão e a permissão de microfone do Windows. {message}";
        if (!automaticallyStarting)
            BackgroundError?.Invoke(message);
    }

    private void ApplyMonitoring()
    {
        if (disposed)
            return;
        if (!MonitorEnabled)
        {
            engine.StopMonitoring();
            MonitorStatus = "Use fones para evitar eco e microfonia.";
            return;
        }
        if (SelectedMonitor is null)
        {
            HandleMonitorFailure("Selecione um fone ou alto-falante para ouvir o microfone.");
            return;
        }
        if (!IsRunning)
        {
            MonitorStatus = "Clique em Iniciar para ouvir sua voz com os ajustes aplicados.";
            return;
        }
        try
        {
            engine.StartMonitoring(SelectedMonitor.Id);
            MonitorStatus = "Retorno ligado: você está ouvindo a saída tratada.";
        }
        catch (Exception exception)
        {
            HandleMonitorFailure($"Não foi possível abrir o retorno. Verifique o fone e tente novamente. {exception.Message}");
        }
    }

    private void HandleMonitorFailure(string message)
    {
        if (disposed)
            return;
        engine.StopMonitoring();
        Set(ref monitorEnabled, false, nameof(MonitorEnabled));
        Notify(nameof(CanSelectMonitor));
        MonitorStatus = message;
    }

    private void UpdateMeters()
    {
        var value = engine.Metrics;
        InputMeter = Math.Clamp(Math.Max(VoiceProcessor.LinearToDb(value.InputPeak), InputMeter - 4), -60, 0);
        OutputMeter = Math.Clamp(Math.Max(VoiceProcessor.LinearToDb(value.OutputPeak), OutputMeter - 4), -60, 0);
        maxProcessingTime = Math.Max(value.ProcessingMilliseconds, maxProcessingTime * 0.96);
        ProcessingTime = IsRunning ? $"{maxProcessingTime:0.00} ms" : "—";
        QueuedAudio = IsRunning ? $"{value.QueueMilliseconds:0} ms" : "—";
        Reduction = IsRunning ? $"{value.ReductionDb:0.0} dB" : "0 dB";
        if (value.InputClipped)
            clippedUntil = DateTime.UtcNow.AddSeconds(2);
        SignalStatus = !IsRunning ? "Aguardando áudio" : Muted ? "Silenciado" : DateTime.UtcNow < clippedUntil
            ? "Entrada saturando: reduza o ganho físico" : value.DroppedBuffers > 0
            ? $"{value.DroppedBuffers} correções de fila; tente um buffer maior" : Bypass ? "Original com limitador de proteção"
            : value.GateClosed ? "Gate fechado: silêncio entre falas" : "Sinal ativo · limite de saída −1 dBFS";
    }

    private void ResetAudio()
    {
        audio = new AudioSettings { Muted = audio.Muted };
        engine.UpdateSettings(audio);
        Notify(string.Empty);
        ScheduleSave();
    }

    private void ReadProfileStatus()
    {
        try
        {
            var profile = store.LoadProfile();
            hasSavedProfile = profile is not null;
            if (profile is not null)
                ProfileStatus = $"Perfil salvo em {profile.SavedAt.LocalDateTime:dd/MM/yyyy HH:mm}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            hasSavedProfile = false;
            ProfileStatus = "Não foi possível ler o perfil de voz. Seus ajustes atuais foram mantidos.";
        }
        ApplyProfileCommand.Refresh();
    }

    private void SaveProfile()
    {
        try
        {
            var profile = store.SaveProfile(audio);
            hasSavedProfile = true;
            ApplyProfileCommand.Refresh();
            ProfileStatus = $"Perfil salvo em {profile.SavedAt.LocalDateTime:dd/MM/yyyy HH:mm}.";
            Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ProfileStatus = "Não foi possível salvar o perfil. Verifique a permissão de escrita da pasta de ajustes.";
        }
    }

    private void ApplyProfile()
    {
        try
        {
            var profile = store.LoadProfile();
            if (profile is null)
            {
                hasSavedProfile = false;
                ApplyProfileCommand.Refresh();
                ProfileStatus = "Nenhum perfil salvo. Ajuste sua voz e clique em Salvar perfil.";
                return;
            }
            audio = profile.Audio with { Muted = audio.Muted, Bypass = audio.Bypass };
            engine.UpdateSettings(audio);
            Notify(string.Empty);
            ProfileStatus = "Perfil reaplicado. Dispositivos, buffer e inicialização foram mantidos.";
            Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            ProfileStatus = "Não foi possível reaplicar o perfil. Seus ajustes atuais foram mantidos.";
        }
    }

    private void ScheduleSave()
    {
        if (refreshing || disposed)
            return;
        SavedStatus = "Salvando ajustes…";
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void Save()
    {
        saveTimer.Stop();
        try
        {
            store.Save(new StudioSettings
            {
                InputDeviceId = SelectedInput?.Id,
                OutputDeviceId = SelectedOutput?.Id,
                MonitorDeviceId = SelectedMonitor?.Id,
                BufferMilliseconds = SelectedBuffer.Milliseconds,
                AutoStartAudio = AutoStartAudio,
                StartInTray = StartInTray,
                Audio = audio with { Muted = false, Bypass = false }
            });
            SavedStatus = "Ajustes salvos automaticamente";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SavedStatus = "Não foi possível salvar. Verifique a permissão de escrita da pasta de ajustes do usuário.";
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        automaticStart?.Cancel();
        meterTimer.Stop();
        saveTimer.Stop();
        Save();
        engine.Dispose();
        IsRunning = false;
    }
}
