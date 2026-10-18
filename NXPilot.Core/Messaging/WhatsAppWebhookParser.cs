// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NXPilot.Messaging;

/// <summary>
/// Converte o webhook da WhatsApp Cloud API em <see cref="IncomingMessage"/>.
///
/// Leitura TOLERANTE de propósito (JsonDocument, nada de classes rígidas): a Meta acrescenta
/// campos com frequência; campo novo é ignorado e campo ausente vira nulo, em vez de quebrar
/// a desserialização. O formato está travado por payloads gravados no smoke test
/// (WhatsAppPayloads) — se a Meta mudar algo que usamos, o build avisa.
/// </summary>
public static class WhatsAppWebhookParser
{
    private static readonly string[] MediaKinds = { "image", "document", "audio", "video", "sticker" };

    public static IReadOnlyList<IncomingMessage> Parse(string json)
    {
        var result = new List<IncomingMessage>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (Str(root, "object") != "whatsapp_business_account") return result;

        foreach (var entry in Arr(root, "entry"))
        foreach (var change in Arr(entry, "changes"))
        {
            if (Str(change, "field") != "messages" || !change.TryGetProperty("value", out var value)) continue;

            // Nome de exibição vem à parte, em "contacts", indexado pelo wa_id.
            var names = Arr(value, "contacts")
                .Where(c => Str(c, "wa_id") != null)
                .ToDictionary(c => Str(c, "wa_id")!, c => c.TryGetProperty("profile", out var p) ? Str(p, "name") : null);

            // Só "messages": "statuses" (entregue/lido) são do que NÓS enviamos — fora do escopo de leitura.
            foreach (var m in Arr(value, "messages"))
            {
                var from = Str(m, "from") ?? "?";
                var type = Str(m, "type") ?? "unknown";
                var text = type == "text" && m.TryGetProperty("text", out var t) ? Str(t, "body") : null;

                var media = new List<MessageMedia>();
                if (MediaKinds.Contains(type) && m.TryGetProperty(type, out var md) && Str(md, "id") is { } mediaId)
                {
                    var caption = Str(md, "caption");
                    media.Add(new MessageMedia(type, mediaId, Str(md, "mime_type"), caption));
                    text ??= caption;
                }

                var ts = long.TryParse(Str(m, "timestamp"), out var unix)
                    ? DateTimeOffset.FromUnixTimeSeconds(unix)
                    : DateTimeOffset.Now;

                // Conversa de grupo (Groups API) traz o id do grupo; 1:1 usa o próprio remetente.
                var chat = Str(m, "group_id") ?? from;

                result.Add(new IncomingMessage(Str(m, "id") ?? Guid.NewGuid().ToString("N"), "WhatsApp",
                    from, names.GetValueOrDefault(from) ?? from, chat, text, ts, media));
            }
        }
        return result;
    }

    /// <summary>
    /// Confere o cabeçalho X-Hub-Signature-256 ("sha256=" + HMAC do corpo com o App Secret).
    /// Sem isso, qualquer um que descobrir a URL do túnel injeta mensagens falsas.
    /// </summary>
    public static bool VerifySignature(byte[] body, string? header, string appSecret)
    {
        if (string.IsNullOrEmpty(header) || !header.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        byte[] received;
        try { received = Convert.FromHexString(header["sha256=".Length..]); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    /// <summary>Assinatura no formato da Meta — usada pelos testes e pelo simulador de webhook.</summary>
    public static string Sign(byte[] body, string appSecret) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body)).ToLowerInvariant();

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
            : Enumerable.Empty<JsonElement>();
}
