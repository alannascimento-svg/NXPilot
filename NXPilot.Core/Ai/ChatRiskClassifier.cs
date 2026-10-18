// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using NXPilot.Risk;

namespace NXPilot.Ai;

/// <summary>
/// 3º degrau da cascata por um provedor remoto no formato chat/completions (OpenAI-compatível):
/// GitHub Copilot, Azure OpenAI, Microsoft 365 (Azure OpenAI com login Entra) e OpenRouter.
/// Só a autenticação muda por provedor — por isso uma classe só, com o "como autenticar"
/// isolado em <see cref="AuthorizeAsync"/>. A ação classificada SAI da máquina: a tela avisa.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ChatRiskClassifier(AiProviderSettings settings) : IRiskClassifier
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public string Name => AiProviderSettings.DisplayName(settings.Kind);

    /// <summary>Confere a configuração; não chama a rede (o primeiro uso mede a latência real).</summary>
    public Task InitializeAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            throw new InvalidOperationException($"{Name}: informe o endpoint.");
        if (settings.Kind != AiProviderKind.Microsoft365 && string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException($"{Name}: informe a chave/token.");
        return Task.CompletedTask;
    }

    public async Task<RiskResult> ClassifyAsync(string action, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var answer = await CompleteAsync(RiskPrompt.System, "Ação: " + action, maxTokens: 5, ct);
        var level = RiskPrompt.Parse(answer, out var understood);
        return new RiskResult(level, understood ? 0.85 : 0, Name, NxText.T("resposta: ", "answer: ") + $"\"{answer.Trim()}\"", sw.Elapsed);
    }

    /// <summary>Chamada de chat genérica (também usada pelo botão "Testar conexão").</summary>
    public async Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "NXPilot/0.1 (Nexus Xdata)");
        await AuthorizeAsync(request, ct);

        // Azure (chave ou conta da empresa): o deployment já define o modelo — "model" fica de fora.
        var azure = settings.Kind is AiProviderKind.AzureOpenAI or AiProviderKind.Microsoft365;
        var messages = new object[] { new { role = "system", content = system }, new { role = "user", content = user } };
        object payload = azure
            ? new { max_tokens = maxTokens, temperature = 0, messages }
            : new { model = settings.Model, max_tokens = maxTokens, temperature = 0, messages };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{Name} respondeu {(int)response.StatusCode}: {Trim(body)}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken ct)
    {
        switch (settings.Kind)
        {
            case AiProviderKind.AzureOpenAI:
                request.Headers.TryAddWithoutValidation("api-key", settings.ApiKey); // Azure usa "api-key", não Bearer
                break;
            case AiProviderKind.Microsoft365:
                var token = await EntraAuthService.GetValidTokenAsync(ct); // renova pelo refresh token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                break;
            case AiProviderKind.OpenRouter:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                // Atribuição de origem do app no OpenRouter (mesmos cabeçalhos do NXProject).
                request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://www.nexusxdata.com");
                request.Headers.TryAddWithoutValidation("X-Title", "NXPilot - Nexus Xdata");
                break;
            default:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                break;
        }
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] + "…" : s;

    public void Dispose() => _http.Dispose();
}
