// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Mail;

/// <summary>
/// Camada de serviço de e-mail — SOMENTE LEITURA: listar, abrir e entregar ao agente como dado
/// não confiável. Não existe (e não deve existir aqui) enviar, responder, apagar ou mover.
/// </summary>
public interface IMailService
{
    MailProvider Provider { get; }
    Task<IReadOnlyList<MailSummary>> ListInboxAsync(MailQuery? query = null, CancellationToken ct = default);
    Task<MailMessage> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Mensagem pronta para o prompt do agente, delimitada como dado não confiável.</summary>
    Task<string> GetForAgentAsync(string id, int maxBodyChars = 8000, CancellationToken ct = default);
}

public sealed class MailService(IMailProvider provider, Action<string>? log = null) : IMailService, IDisposable
{
    public MailProvider Provider => provider.Provider;

    /// <summary>Monta o serviço do provedor com o token renovado automaticamente.</summary>
    public static MailService Create(MailProvider kind, OAuthTokenProvider tokens, Action<string>? log = null) => new(kind switch
    {
        MailProvider.Outlook => new GraphMailProvider(tokens.GetAccessTokenAsync),
        MailProvider.Gmail => new GmailProvider(tokens.GetAccessTokenAsync),
        _ => throw new NotSupportedException(kind.ToString()),
    }, log);

    public async Task<IReadOnlyList<MailSummary>> ListInboxAsync(MailQuery? query = null, CancellationToken ct = default)
    {
        var list = await provider.ListAsync(query ?? new MailQuery(), ct);
        log?.Invoke($"E-mail ({Provider}): {list.Count} mensagem(ns) listada(s)");
        return list;
    }

    public async Task<MailMessage> GetAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id vazio.", nameof(id));
        var m = await provider.GetAsync(id, ct);
        log?.Invoke($"E-mail ({Provider}): aberto \"{m.Subject}\" de {m.From}");
        return m;
    }

    public async Task<string> GetForAgentAsync(string id, int maxBodyChars = 8000, CancellationToken ct = default) =>
        (await GetAsync(id, ct)).ToAgentContext(maxBodyChars);

    public void Dispose() => provider.Dispose();
}
