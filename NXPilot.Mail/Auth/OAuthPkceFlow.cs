// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NXPilot.Mail;

/// <summary>Aplicativo OAuth registrado pela empresa/usuário (Entra ID ou Google Cloud).</summary>
public sealed record OAuthClientConfig(string Name, string AuthorizeUrl, string TokenUrl, string ClientId, string? ClientSecret,
    IReadOnlyList<string> Scopes, string RedirectHost, IReadOnlyDictionary<string, string>? ExtraAuthorizeParams = null)
{
    /// <summary>
    /// Outlook / Microsoft 365 pelo Microsoft Graph, só leitura (Mail.Read). Registro de app no
    /// Entra ID como "Mobile and desktop", redirect http://localhost. Tenant "common" aceita
    /// conta da empresa e pessoal (se o registro permitir); use o ID do tenant para restringir.
    /// </summary>
    public static OAuthClientConfig Outlook(string clientId, string tenant = "common") => new("Outlook",
        $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize",
        $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
        clientId, null, ["https://graph.microsoft.com/Mail.Read", "offline_access"], "localhost");

    /// <summary>
    /// Gmail API, só leitura (gmail.readonly). Cliente OAuth do tipo "Desktop app" no Google
    /// Cloud (o client secret de app desktop não é confidencial, mas o Google exige). É escopo
    /// "restrito": uso próprio/da empresa com usuários de teste; distribuição pública exige
    /// verificação do Google.
    /// </summary>
    public static OAuthClientConfig Gmail(string clientId, string clientSecret) => new("Gmail",
        "https://accounts.google.com/o/oauth2/v2/auth", "https://oauth2.googleapis.com/token",
        clientId, clientSecret, ["https://www.googleapis.com/auth/gmail.readonly"], "127.0.0.1",
        new Dictionary<string, string> { ["access_type"] = "offline", ["prompt"] = "consent" }); // sem isso não vem refresh token
}

public sealed record OAuthToken(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt)
{
    public bool IsFresh(TimeSpan margin) => AccessToken.Length > 0 && ExpiresAt - margin > DateTimeOffset.UtcNow;
    public override string ToString() => $"token até {ExpiresAt:u}{(RefreshToken != null ? " (renovável)" : "")}";
}

/// <summary>Onde o token fica entre execuções (no NXPilot: arquivo cifrado com DPAPI).</summary>
public interface IOAuthTokenStore
{
    Task<OAuthToken?> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(OAuthToken token, CancellationToken ct = default);
}

public sealed class InMemoryTokenStore(OAuthToken? token = null) : IOAuthTokenStore
{
    public OAuthToken? Token { get; private set; } = token;
    public Task<OAuthToken?> LoadAsync(CancellationToken ct = default) => Task.FromResult(Token);
    public Task SaveAsync(OAuthToken token, CancellationToken ct = default) { Token = token; return Task.CompletedTask; }
}

/// <summary>
/// Login de app desktop (RFC 8252): abre o navegador na página do provedor, recebe o código
/// num servidor de uma requisição só, no loopback e em porta aleatória, e troca o código pelo
/// token com PKCE (S256). A senha só é digitada na página do provedor — o NXPilot nunca a vê.
/// </summary>
public sealed class OAuthPkceFlow(OAuthClientConfig config, HttpMessageHandler? handler = null)
{
    private readonly HttpClient _http = handler == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : new HttpClient(handler, disposeHandler: false);

    public OAuthClientConfig Config => config;

    public static (string Verifier, string Challenge) CreatePkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    /// <param name="openBrowser">Abre a URL de login (padrão: navegador do Windows). Os testes trocam.</param>
    public async Task<OAuthToken> LoginAsync(Func<Uri, Task>? openBrowser = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var (verifier, challenge) = CreatePkce();
        var state = Base64Url(RandomNumberGenerator.GetBytes(16));

        var v4 = new TcpListener(IPAddress.Loopback, 0);
        v4.Start();
        var port = ((IPEndPoint)v4.LocalEndpoint).Port;
        TcpListener? v6 = null;
        if (config.RedirectHost == "localhost") // o navegador pode resolver localhost para ::1
        {
            try { v6 = new TcpListener(IPAddress.IPv6Loopback, port); v6.Start(); } catch (SocketException) { v6 = null; }
        }

        try
        {
            var redirect = $"http://{config.RedirectHost}:{port}/";
            var query = new Dictionary<string, string>
            {
                ["client_id"] = config.ClientId, ["response_type"] = "code", ["redirect_uri"] = redirect,
                ["scope"] = string.Join(' ', config.Scopes), ["state"] = state,
                ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            };
            foreach (var (k, v) in config.ExtraAuthorizeParams ?? new Dictionary<string, string>()) query[k] = v;
            var url = new Uri(config.AuthorizeUrl + "?" + string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}")));

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(timeout ?? TimeSpan.FromMinutes(5));
            var accept = new List<Task<TcpClient>> { v4.AcceptTcpClientAsync(limit.Token).AsTask() };
            if (v6 != null) accept.Add(v6.AcceptTcpClientAsync(limit.Token).AsTask());

            await (openBrowser ?? OpenInBrowser)(url);
            TcpClient client;
            try { client = await await Task.WhenAny(accept); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { throw new MailException($"O login no {config.Name} não foi concluído no navegador a tempo."); }

            Dictionary<string, string> result;
            using (client)
            {
                result = await ReadRedirectAsync(client, limit.Token);
            }
            if (result.TryGetValue("error", out var error))
                throw new MailException($"Login no {config.Name} recusado: {error} {result.GetValueOrDefault("error_description")}".Trim());
            if (result.GetValueOrDefault("state") != state)
                throw new MailException("Resposta de login com 'state' diferente — ignorada (possível ataque).");
            if (!result.TryGetValue("code", out var code)) throw new MailException("Resposta de login sem código.");

            return await TokenAsync(new()
            {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier,
            }, null, ct);
        }
        finally
        {
            v4.Stop();
            v6?.Stop();
        }
    }

    /// <summary>Renova o token. Se o provedor não devolver novo refresh token, o anterior continua valendo.</summary>
    public Task<OAuthToken> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        TokenAsync(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken }, refreshToken, ct);

    private async Task<OAuthToken> TokenAsync(Dictionary<string, string> form, string? previousRefresh, CancellationToken ct)
    {
        form["client_id"] = config.ClientId;
        if (config.ClientSecret != null) form["client_secret"] = config.ClientSecret;
        if (form["grant_type"] == "refresh_token" || config.RedirectHost == "localhost") form["scope"] = string.Join(' ', config.Scopes);
        using var resp = await _http.PostAsync(config.TokenUrl, new FormUrlEncodedContent(form), ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text.Length == 0 ? "{}" : text);
        var root = doc.RootElement;
        string? S(string n) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (!resp.IsSuccessStatusCode || S("access_token") is not { } access)
            throw new MailException($"{config.Name}: falha ao obter token ({(int)resp.StatusCode} {S("error")}): {S("error_description")}".Trim(), (int)resp.StatusCode);
        var expires = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var secs) ? secs : 3600;
        return new OAuthToken(access, S("refresh_token") ?? previousRefresh, DateTimeOffset.UtcNow.AddSeconds(expires));
    }

    /// <summary>Lê "GET /?code=…&amp;state=… HTTP/1.1", responde uma página simples e devolve os parâmetros.</summary>
    private static async Task<Dictionary<string, string>> ReadRedirectAsync(TcpClient client, CancellationToken ct)
    {
        var stream = client.GetStream();
        var buffer = new byte[8192];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0) break;
            read += n;
            if (Encoding.ASCII.GetString(buffer, 0, read).Contains("\r\n\r\n")) break;
        }
        var requestLine = Encoding.ASCII.GetString(buffer, 0, read).Split("\r\n")[0];
        var target = requestLine.Split(' ') is { Length: >= 2 } p ? p[1] : "/";
        var q = target.Contains('?') ? target[(target.IndexOf('?') + 1)..] : "";
        var values = q.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(kv => kv.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => kv.Length > 1 ? Uri.UnescapeDataString(kv[1].Replace('+', ' ')) : "");

        var ok = values.ContainsKey("code");
        var html = $"<html><head><meta charset=\"utf-8\"><title>NXPilot</title></head><body style=\"font-family:Segoe UI;margin:3em\">" +
                   $"<h2>{(ok ? "Login concluído" : "Login não concluído")}</h2><p>Pode fechar esta janela e voltar ao NXPilot.</p></body></html>";
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(body, ct);
        return values;
    }

    private static Task OpenInBrowser(Uri url)
    {
        Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Entrega um access token válido: usa o salvo, renova quando falta pouco, pede login se não houver.</summary>
public sealed class OAuthTokenProvider(OAuthPkceFlow flow, IOAuthTokenStore store)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var token = await store.LoadAsync(ct)
                        ?? throw new MailException($"Sem login no {flow.Config.Name}. Faça o login primeiro.");
            if (token.IsFresh(TimeSpan.FromMinutes(2))) return token.AccessToken;
            if (token.RefreshToken == null) throw new MailException($"O login no {flow.Config.Name} expirou. Entre de novo.");
            token = await flow.RefreshAsync(token.RefreshToken, ct);
            await store.SaveAsync(token, ct);
            return token.AccessToken;
        }
        finally { _lock.Release(); }
    }

    public async Task LoginAsync(Func<Uri, Task>? openBrowser = null, CancellationToken ct = default) =>
        await store.SaveAsync(await flow.LoginAsync(openBrowser, ct: ct), ct);
}
