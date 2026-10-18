// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Messaging;

/// <summary>Imagem, áudio ou documento anexado. <see cref="LocalPath"/> só existe depois do download.</summary>
public sealed record MessageMedia(string Kind, string MediaId, string? MimeType, string? Caption)
{
    public string? LocalPath { get; init; }
}

/// <summary>
/// Mensagem recebida, já no formato do NXPilot — nada aqui conhece o JSON da Meta. Se a
/// Meta mudar o webhook, muda só o adaptador; triagem, relatório e tela continuam iguais.
/// </summary>
public sealed record IncomingMessage(
    string Id,
    string Channel,
    string FromId,
    string FromName,
    string ChatId,
    string? Text,
    DateTimeOffset Timestamp,
    IReadOnlyList<MessageMedia> Media);

/// <summary>
/// Fachada de uma fonte de mensagens (WhatsApp oficial, simulador; no futuro, e-mail ou
/// Teams). Somente LEITURA: enviar fica em outra interface, ligada à parte e por opção.
/// </summary>
public interface IMessageSource : IAsyncDisposable
{
    string Name { get; }
    bool IsRunning { get; }

    event Action<IncomingMessage>? MessageReceived;

    /// <summary>Texto para a tela: "escutando em …", erros de configuração etc.</summary>
    event Action<string>? StatusChanged;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}

public enum MessageRelevance { Ignorar, Relevante, Prioritario }

public sealed record TriageResult(MessageRelevance Relevance, string Reason, string Engine);
