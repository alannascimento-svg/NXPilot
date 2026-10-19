// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NXPilot.Desktop.Services;
using NXPilot.Messaging;
using static NXPilot.NxText;

namespace NXPilot.Desktop.ViewModels;

public sealed record MessageRow(string Time, string From, string? Text, string? MediaInfo, MessageRelevance Relevance, string Reason)
{
    /// <summary>Motivo da triagem + anexo, só quando houver anexo.</summary>
    public string Detail => MediaInfo == null ? Reason : $"{Reason} · 📎 {MediaInfo}";

    public string RelevanceLabel => Relevance switch
    {
        MessageRelevance.Prioritario => NxText.T("Prioritário", "Priority", "Prioritario"),
        MessageRelevance.Relevante => NxText.T("Relevante", "Relevant", "Relevante"),
        _ => NxText.T("Ignorar", "Ignore", "Ignorar")
    };
}

/// <summary>
/// Canal de mensagens (opcional, desligado por padrão). Somente LEITURA: o NXPilot recebe,
/// tria em cascata e registra; não envia nada por este canal. Duas fontes atrás da mesma
/// fachada <see cref="IMessageSource"/>: simulador (sem conta) e número-robô pela API oficial.
/// </summary>
public sealed partial class ChannelsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly WhatsAppCloudOptions _options = WhatsAppCloudOptions.Load();
    private IMessageSource? _source;

    public ChannelsViewModel(MainViewModel main)
    {
        _main = main;
        _phoneNumberId = _options.PhoneNumberId;
        _apiVersion = _options.ApiVersion;
        _listenPort = _options.ListenPort;
        _prioritySenders = string.Join(", ", _options.PrioritySenders);
        _keywords = string.Join(", ", _options.Keywords);
        LanguageService.LanguageChanged += () => OnPropertyChanged(nameof(SecretsState));
    }

    public ObservableCollection<MessageRow> Messages { get; } = new();

    [ObservableProperty] private bool _useOfficialApi;
    [ObservableProperty] private string _phoneNumberId;
    [ObservableProperty] private string _apiVersion;
    [ObservableProperty] private int _listenPort;
    [ObservableProperty] private string _prioritySenders;
    [ObservableProperty] private string _keywords;
    [ObservableProperty] private string _status = T("Canal desligado.", "Channel off.", "Canal apagado.");
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _priorityCount;
    [ObservableProperty] private int _relevantCount;
    [ObservableProperty] private int _ignoredCount;

    public string VerifyToken => _options.VerifyToken;
    public string TunnelCommand => $"cloudflared tunnel --url http://localhost:{ListenPort}";
    public string SecretsState => _options.AccessToken.Length > 0
        ? T("Token e App Secret salvos (cifrados no seu perfil).", "Token and App Secret saved (encrypted in your profile).", "Token y App Secret guardados (cifrados en su perfil).")
        : T("Token e App Secret ainda não informados.", "Token and App Secret not provided yet.", "Token y App Secret aún no informados.");
    public bool IsSimulator => !UseOfficialApi;

    partial void OnUseOfficialApiChanged(bool value) => OnPropertyChanged(nameof(IsSimulator));
    partial void OnListenPortChanged(int value) => OnPropertyChanged(nameof(TunnelCommand));

    /// <summary>Segredos chegam do code-behind (PasswordBox não tem binding, de propósito).</summary>
    public void SetSecrets(string? accessToken, string? appSecret)
    {
        if (!string.IsNullOrEmpty(accessToken)) _options.AccessToken = accessToken;
        if (!string.IsNullOrEmpty(appSecret)) _options.AppSecret = appSecret;
    }

    [RelayCommand]
    private void Save()
    {
        _options.PhoneNumberId = PhoneNumberId.Trim();
        _options.ApiVersion = string.IsNullOrWhiteSpace(ApiVersion) ? WhatsAppCloudOptions.DefaultApiVersion : ApiVersion.Trim();
        _options.ListenPort = ListenPort;
        _options.PrioritySenders = Split(PrioritySenders);
        _options.Keywords = Split(Keywords);
        try
        {
            _options.Save();
            Status = T("Configuração salva.", "Settings saved.", "Configuración guardada.");
        }
        catch (Exception ex) { Status = T("Não salvou: ", "Not saved: ", "No se guardó: ") + ex.Message; }
        OnPropertyChanged(nameof(SecretsState));
    }

    [RelayCommand]
    private async Task Start()
    {
        if (IsRunning) return;
        Save();
        _source = UseOfficialApi
            ? new WhatsAppCloudSource(_options, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "media"))
            : new SimulatedMessageSource();
        // Eventos chegam na thread do listener: tudo que toca a tela volta para a UI.
        _source.StatusChanged += s => OnUi(() => Status = s);
        _source.MessageReceived += m => OnUi(() => Receive(m));
        try
        {
            await _source.StartAsync();
            IsRunning = true;
            _main.AddLog(T("Canal", "Channel", "Canal"), "info", T($"{_source.Name} ligado (somente leitura).", $"{_source.Name} on (read-only).", $"{_source.Name} encendido (solo lectura)."));
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            await _source.DisposeAsync();
            _source = null;
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (_source == null) return;
        await _source.DisposeAsync();
        _main.AddLog(T("Canal", "Channel", "Canal"), "info", T($"{_source.Name} desligado.", $"{_source.Name} off.", $"{_source.Name} apagado."));
        _source = null;
        IsRunning = false;
    }

    [RelayCommand]
    private void EmitTest()
    {
        if (_source is SimulatedMessageSource sim) sim.EmitNext();
        else Status = T("Ligue o canal no modo Simulador para receber mensagens de teste.", "Turn the channel on in Simulator mode to receive test messages.", "Encienda el canal en modo Simulador para recibir mensajes de prueba.");
    }

    [RelayCommand]
    private void CopyVerifyToken()
    {
        try { Clipboard.SetText(VerifyToken); Status = T("Verify token copiado — cole no painel de Webhooks da Meta.", "Verify token copied — paste it in the Meta Webhooks panel.", "Verify token copiado — péguelo en el panel de Webhooks de Meta."); }
        catch { /* área de transferência ocupada */ }
    }

    /// <summary>Degrau 1 da cascata (regras). ONNX por assunto e o relatório com visão entram depois.</summary>
    private void Receive(IncomingMessage m)
    {
        var triage = new RuleMessageTriage(Split(PrioritySenders), Split(Keywords)).Classify(m);
        var media = m.Media.Count == 0 ? null
            : string.Join(", ", m.Media.Select(x => x.LocalPath != null ? T($"{x.Kind} salvo", $"{x.Kind} saved", $"{x.Kind} guardado") : T($"{x.Kind} (não baixado)", $"{x.Kind} (not downloaded)", $"{x.Kind} (no descargado)")));
        Messages.Insert(0, new MessageRow(m.Timestamp.LocalDateTime.ToString("HH:mm:ss"), m.FromName, m.Text, media, triage.Relevance, triage.Reason));

        switch (triage.Relevance)
        {
            case MessageRelevance.Prioritario:
                PriorityCount++;
                _main.Notifications.Notify(NotificationKind.Message, T($"Mensagem prioritária · {m.FromName}", $"Priority message · {m.FromName}", $"Mensaje prioritario · {m.FromName}"), m.Text ?? media ?? "");
                break;
            case MessageRelevance.Relevante: RelevantCount++; break;
            default: IgnoredCount++; break;
        }
        _main.AddLog("WhatsApp", triage.Relevance.ToString().ToLowerInvariant(), $"{m.FromName}: {m.Text ?? media} ({triage.Reason})");
    }

    private static List<string> Split(string s) =>
        s.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static void OnUi(Action a) => Application.Current?.Dispatcher.BeginInvoke(a);
}
