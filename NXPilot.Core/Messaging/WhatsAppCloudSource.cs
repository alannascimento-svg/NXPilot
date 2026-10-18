// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NXPilot.Messaging;

/// <summary>
/// Número-robô pela API OFICIAL da Meta (WhatsApp Cloud API), somente leitura.
///
/// A Meta entrega as mensagens por webhook HTTPS público; o NXPilot roda no desktop, então
/// escuta em http://localhost:{porta}/whatsapp/ e um túnel (ex.: cloudflared) publica essa
/// porta. GET = verificação do webhook (hub.challenge); POST = mensagens, aceitas só com
/// assinatura válida do App Secret. Imagens e documentos são baixados pela Graph API.
/// </summary>
public sealed class WhatsAppCloudSource(WhatsAppCloudOptions options, string mediaFolder) : IMessageSource
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly HttpClient _graph = new() { Timeout = TimeSpan.FromSeconds(60) };

    public string Name => "WhatsApp (API oficial)";
    public bool IsRunning => _listener?.IsListening == true;

    public event Action<IncomingMessage>? MessageReceived;
    public event Action<string>? StatusChanged;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;
        if (!options.IsConfigured)
            throw new InvalidOperationException("Preencha Phone Number ID, Access Token e App Secret antes de iniciar.");

        // Prefixo em localhost não exige administrador (http.sys reserva só para outros hosts).
        _listener = new HttpListener();
        _listener.Prefixes.Add(options.LocalWebhookUrl);
        _listener.Start();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = AcceptLoopAsync(_cts.Token);
        StatusChanged?.Invoke($"Escutando em {options.LocalWebhookUrl} — publique com um túnel HTTPS e cadastre a URL no painel da Meta.");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); _listener?.Close(); } catch { /* já parado */ }
        _listener = null;
        StatusChanged?.Invoke("Parado.");
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; } // Stop() encerra o GetContextAsync com exceção
            _ = HandleAsync(ctx);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.HttpMethod == "GET")
            {
                // Verificação: a Meta manda hub.mode=subscribe + hub.verify_token + hub.challenge.
                var q = ctx.Request.QueryString;
                var ok = q["hub.mode"] == "subscribe" && q["hub.verify_token"] == options.VerifyToken;
                await RespondAsync(ctx, ok ? 200 : 403, ok ? q["hub.challenge"] ?? "" : "verify_token inválido");
                StatusChanged?.Invoke(ok ? "Webhook verificado pela Meta." : "Tentativa de verificação com verify_token errado.");
                return;
            }

            using var ms = new MemoryStream();
            await ctx.Request.InputStream.CopyToAsync(ms);
            var body = ms.ToArray();
            if (!WhatsAppWebhookParser.VerifySignature(body, ctx.Request.Headers["X-Hub-Signature-256"], options.AppSecret))
            {
                await RespondAsync(ctx, 401, "assinatura inválida");
                StatusChanged?.Invoke("Webhook rejeitado: assinatura inválida (App Secret diferente ou chamada falsa).");
                return;
            }

            // Responde 200 já: a Meta reenvia se demorar, e o download de mídia pode levar segundos.
            await RespondAsync(ctx, 200, "ok");
            foreach (var msg in WhatsAppWebhookParser.Parse(Encoding.UTF8.GetString(body)))
                MessageReceived?.Invoke(await DownloadMediaAsync(msg));
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Erro ao processar webhook: " + ex.Message);
            try { await RespondAsync(ctx, 500, "erro"); } catch { /* conexão já fechada */ }
        }
    }

    private static async Task RespondAsync(HttpListenerContext ctx, int status, string text)
    {
        ctx.Response.StatusCode = status;
        var bytes = Encoding.UTF8.GetBytes(text);
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    /// <summary>
    /// Dois passos da Graph API: GET /{versão}/{media-id} devolve uma URL temporária; GET nessa
    /// URL (com o mesmo token) devolve o arquivo. Falha no download não perde a mensagem.
    /// </summary>
    private async Task<IncomingMessage> DownloadMediaAsync(IncomingMessage msg)
    {
        if (msg.Media.Count == 0) return msg;
        var downloaded = new List<MessageMedia>();
        foreach (var m in msg.Media)
        {
            try
            {
                using var meta = new HttpRequestMessage(HttpMethod.Get, $"https://graph.facebook.com/{options.ApiVersion}/{m.MediaId}");
                meta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
                using var metaResp = await _graph.SendAsync(meta);
                metaResp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await metaResp.Content.ReadAsStringAsync());
                var url = doc.RootElement.GetProperty("url").GetString()!;

                using var file = new HttpRequestMessage(HttpMethod.Get, url);
                file.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
                using var fileResp = await _graph.SendAsync(file);
                fileResp.EnsureSuccessStatusCode();

                Directory.CreateDirectory(mediaFolder);
                var ext = m.MimeType?.Split('/').LastOrDefault()?.Split(';')[0] ?? "bin";
                var path = Path.Combine(mediaFolder, $"{m.MediaId}.{ext}");
                await File.WriteAllBytesAsync(path, await fileResp.Content.ReadAsByteArrayAsync());
                downloaded.Add(m with { LocalPath = path });
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Não baixou a mídia {m.MediaId}: {ex.Message}");
                downloaded.Add(m);
            }
        }
        return msg with { Media = downloaded };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _graph.Dispose();
    }
}
