// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NXPilot.Mail;

/// <summary>Camada baixa por provedor — só leitura (listar e abrir).</summary>
public interface IMailProvider : IDisposable
{
    MailProvider Provider { get; }
    Task<IReadOnlyList<MailSummary>> ListAsync(MailQuery query, CancellationToken ct = default);
    Task<MailMessage> GetAsync(string id, CancellationToken ct = default);
}

/// <summary>Base comum: HttpClient, Bearer renovado a cada chamada e erro sem o token.</summary>
public abstract class MailProviderBase(Func<CancellationToken, Task<string>> accessToken, HttpMessageHandler? handler) : IMailProvider
{
    private readonly HttpClient _http = handler == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(60) } : new HttpClient(handler, disposeHandler: false);
    public abstract MailProvider Provider { get; }
    public abstract Task<IReadOnlyList<MailSummary>> ListAsync(MailQuery query, CancellationToken ct = default);
    public abstract Task<MailMessage> GetAsync(string id, CancellationToken ct = default);

    protected async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct, params (string Name, string Value)[] headers)
    {
        var token = await accessToken(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        foreach (var (n, v) in headers) req.Headers.TryAddWithoutValidation(n, v);
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (resp.IsSuccessStatusCode) return JsonDocument.Parse(text);
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var e))
                message = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m) ? m.GetString() : e.ToString();
        }
        catch (JsonException) { }
        var status = (int)resp.StatusCode;
        var hint = status switch { 401 => " Login expirado ou revogado: entre de novo.", 403 => " Sem permissão de leitura de e-mail.", _ => "" };
        message ??= text.Length > 300 ? text[..300] : text;
        if (token.Length > 0) message = message.Replace(token, "***");
        throw new MailException($"{Provider} HTTP {status}: {message}{hint}", status);
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Outlook / Microsoft 365 pelo Microsoft Graph v1.0 (escopo Mail.Read). Caixa de entrada;
/// corpo pedido já em texto (Prefer: outlook.body-content-type="text").
/// </summary>
public sealed class GraphMailProvider(Func<CancellationToken, Task<string>> accessToken, HttpMessageHandler? handler = null)
    : MailProviderBase(accessToken, handler)
{
    private const string Api = "https://graph.microsoft.com/v1.0/me";
    public override MailProvider Provider => MailProvider.Outlook;

    public override async Task<IReadOnlyList<MailSummary>> ListAsync(MailQuery query, CancellationToken ct = default)
    {
        var p = new List<string>
        {
            $"$top={Math.Clamp(query.Max, 1, 100)}",
            "$select=id,subject,from,receivedDateTime,bodyPreview,isRead,hasAttachments",
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // $search não combina com $orderby/$filter no Graph: busca primeiro, filtra aqui.
            p.Add("$search=" + Uri.EscapeDataString($"\"{query.Search.Replace("\"", "")}\""));
        }
        else
        {
            var filters = new List<string>();
            if (query.UnreadOnly) filters.Add("isRead eq false");
            if (query.Since is { } since) filters.Add($"receivedDateTime ge {since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");
            if (filters.Count > 0) p.Add("$filter=" + Uri.EscapeDataString(string.Join(" and ", filters)));
            p.Add("$orderby=" + Uri.EscapeDataString("receivedDateTime desc"));
        }
        using var doc = await GetJsonAsync($"{Api}/mailFolders/inbox/messages?{string.Join('&', p)}", ct);
        var list = doc.RootElement.GetProperty("value").EnumerateArray().Select(m => new MailSummary(
            m.GetProperty("id").GetString()!, Provider, Address(m, "from"), Str(m, "subject"),
            DateTimeOffset.Parse(Str(m, "receivedDateTime"), CultureInfo.InvariantCulture), Str(m, "bodyPreview"),
            m.TryGetProperty("isRead", out var r) && r.GetBoolean(), m.TryGetProperty("hasAttachments", out var a) && a.GetBoolean()));
        if (!string.IsNullOrWhiteSpace(query.Search))
            list = list.Where(m => (!query.UnreadOnly || !m.IsRead) && (query.Since == null || m.ReceivedAt >= query.Since));
        return list.ToList();
    }

    public override async Task<MailMessage> GetAsync(string id, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync(
            $"{Api}/messages/{Uri.EscapeDataString(id)}?$select=id,subject,from,toRecipients,receivedDateTime,body,hasAttachments,webLink",
            ct, ("Prefer", "outlook.body-content-type=\"text\""));
        var m = doc.RootElement;
        var body = m.TryGetProperty("body", out var b) ? b : default;
        var content = body.ValueKind == JsonValueKind.Object ? Str(body, "content") : "";
        if (body.ValueKind == JsonValueKind.Object && Str(body, "contentType").Equals("html", StringComparison.OrdinalIgnoreCase)) content = HtmlText.ToText(content);

        var attachments = new List<MailAttachmentInfo>();
        if (m.TryGetProperty("hasAttachments", out var has) && has.GetBoolean())
        {
            using var att = await GetJsonAsync($"{Api}/messages/{Uri.EscapeDataString(id)}/attachments?$select=name,contentType,size", ct);
            attachments.AddRange(att.RootElement.GetProperty("value").EnumerateArray()
                .Select(a => new MailAttachmentInfo(Str(a, "name"), Str(a, "contentType"), a.TryGetProperty("size", out var s) ? s.GetInt64() : null)));
        }
        var to = m.TryGetProperty("toRecipients", out var t) ? t.EnumerateArray().Select(r => Address(r, null)).ToList() : new List<string>();
        return new MailMessage(Str(m, "id"), Provider, Address(m, "from"), to, Str(m, "subject"),
            DateTimeOffset.Parse(Str(m, "receivedDateTime"), CultureInfo.InvariantCulture), content.Trim(), attachments, Str(m, "webLink"));
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    /// <summary>{"from":{"emailAddress":{"name","address"}}} → "Nome &lt;email&gt;".</summary>
    private static string Address(JsonElement e, string? property)
    {
        var holder = property == null ? e : e.TryGetProperty(property, out var p) ? p : default;
        if (holder.ValueKind != JsonValueKind.Object || !holder.TryGetProperty("emailAddress", out var ea)) return "";
        var name = Str(ea, "name");
        var address = Str(ea, "address");
        return name.Length > 0 && name != address ? $"{name} <{address}>" : address;
    }
}

/// <summary>
/// Gmail API v1 (escopo gmail.readonly). Listagem = ids (messages.list com a busca do Gmail) +
/// metadados de cada um; mensagem completa decodifica o MIME (base64url), preferindo text/plain.
/// </summary>
public sealed class GmailProvider(Func<CancellationToken, Task<string>> accessToken, HttpMessageHandler? handler = null)
    : MailProviderBase(accessToken, handler)
{
    private const string Api = "https://gmail.googleapis.com/gmail/v1/users/me";
    public override MailProvider Provider => MailProvider.Gmail;

    public override async Task<IReadOnlyList<MailSummary>> ListAsync(MailQuery query, CancellationToken ct = default)
    {
        var q = new List<string> { "in:inbox" };
        if (query.UnreadOnly) q.Add("is:unread");
        if (query.Since is { } since) q.Add($"after:{since.ToUnixTimeSeconds()}");
        if (!string.IsNullOrWhiteSpace(query.Search)) q.Add(query.Search.Trim());
        using var ids = await GetJsonAsync($"{Api}/messages?maxResults={Math.Clamp(query.Max, 1, 100)}&q={Uri.EscapeDataString(string.Join(' ', q))}", ct);
        if (!ids.RootElement.TryGetProperty("messages", out var messages)) return Array.Empty<MailSummary>();

        var list = new List<MailSummary>();
        foreach (var id in messages.EnumerateArray().Select(m => m.GetProperty("id").GetString()!))
        {
            using var doc = await GetJsonAsync($"{Api}/messages/{id}?format=metadata&metadataHeaders=From&metadataHeaders=Subject", ct);
            var m = doc.RootElement;
            var labels = Labels(m);
            var hasAttachment = m.TryGetProperty("payload", out var payload) && Parts(payload).Any(p => FileName(p).Length > 0);
            list.Add(new MailSummary(id, Provider, Header(m, "From"), Header(m, "Subject"), Received(m),
                System.Net.WebUtility.HtmlDecode(Str(m, "snippet")), !labels.Contains("UNREAD"), hasAttachment));
        }
        return list;
    }

    public override async Task<MailMessage> GetAsync(string id, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"{Api}/messages/{Uri.EscapeDataString(id)}?format=full", ct);
        var m = doc.RootElement;
        var payload = m.GetProperty("payload");
        var parts = Parts(payload).ToList();
        var plain = parts.FirstOrDefault(p => MimeType(p) == "text/plain" && FileName(p).Length == 0);
        var html = parts.FirstOrDefault(p => MimeType(p) == "text/html" && FileName(p).Length == 0);
        var body = plain.ValueKind != JsonValueKind.Undefined ? Data(plain)
                 : html.ValueKind != JsonValueKind.Undefined ? HtmlText.ToText(Data(html)) : "";
        var attachments = parts.Where(p => FileName(p).Length > 0)
            .Select(p => new MailAttachmentInfo(FileName(p), MimeType(p),
                p.TryGetProperty("body", out var b) && b.TryGetProperty("size", out var s) ? s.GetInt64() : null)).ToList();
        var to = Header(m, "To").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return new MailMessage(id, Provider, Header(m, "From"), to, Header(m, "Subject"), Received(m), body.Replace("\r", "").Trim(),
            attachments, $"https://mail.google.com/mail/u/0/#all/{Str(m, "threadId")}");
    }

    private static IEnumerable<JsonElement> Parts(JsonElement part)
    {
        yield return part;
        if (part.TryGetProperty("parts", out var children))
            foreach (var c in children.EnumerateArray())
                foreach (var p in Parts(c)) yield return p;
    }

    private static string MimeType(JsonElement p) => Str(p, "mimeType").ToLowerInvariant();
    private static string FileName(JsonElement p) => Str(p, "filename");

    /// <summary>body.data em base64url (sem padding).</summary>
    private static string Data(JsonElement part)
    {
        if (!part.TryGetProperty("body", out var b) || Str(b, "data") is not { Length: > 0 } data) return "";
        var s = data.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }

    private static HashSet<string> Labels(JsonElement m) =>
        m.TryGetProperty("labelIds", out var l) ? l.EnumerateArray().Select(x => x.GetString() ?? "").ToHashSet() : new HashSet<string>();

    private static DateTimeOffset Received(JsonElement m) =>
        long.TryParse(Str(m, "internalDate"), out var ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : DateTimeOffset.MinValue;

    private static string Header(JsonElement m, string name) =>
        m.TryGetProperty("payload", out var p) && p.TryGetProperty("headers", out var hs)
            ? hs.EnumerateArray().Where(h => Str(h, "name").Equals(name, StringComparison.OrdinalIgnoreCase)).Select(h => Str(h, "value")).FirstOrDefault() ?? ""
            : "";

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
}
