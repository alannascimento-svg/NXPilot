// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NXPilot.Databricks;

/// <summary>Git folder do workspace (Repos API): caminho, URL do remoto, branch e commit atual.</summary>
public sealed record DatabricksRepo(long Id, string Path, string? Url, string? Provider, string? Branch, string? HeadCommitId);

/// <summary>
/// Git folders do Databricks (/api/2.0/repos): é por aqui que o código commitado no GitHub/DevOps
/// chega ao workspace — o Databricks faz o checkout/pull da branch. Só precisa de host e token.
/// </summary>
public sealed class DatabricksReposClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _token;

    public DatabricksReposClient(DatabricksConnection connection, HttpMessageHandler? handler = null)
    {
        if (connection.Host.Length <= "https://".Length || connection.Token.Length == 0)
            throw new ArgumentException("Informe host e token do Databricks.", nameof(connection));
        _token = connection.Token;
        _http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.BaseAddress = new Uri(connection.Host + "/");
        _http.Timeout = TimeSpan.FromMinutes(5); // checkout de repositório grande demora
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
    }

    /// <summary>Acha o Git folder pelo caminho no workspace (ex.: /Repos/eu@empresa.com/projeto ou /Workspace/Users/…).</summary>
    public async Task<DatabricksRepo?> FindByPathAsync(string workspacePath, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/2.0/workspace/get-status?path={Uri.EscapeDataString(workspacePath)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        using var doc = await ReadAsync(resp, ct);
        if (!doc.RootElement.TryGetProperty("object_type", out var t) || t.GetString() != "REPO") return null;
        return await GetAsync(doc.RootElement.GetProperty("object_id").GetInt64(), ct);
    }

    public async Task<DatabricksRepo> GetAsync(long id, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/2.0/repos/{id}", ct);
        using var doc = await ReadAsync(resp, ct);
        return Map(doc.RootElement);
    }

    /// <summary>Muda para a branch e traz o último commit dela (PATCH /api/2.0/repos/{id}).</summary>
    public async Task<DatabricksRepo> CheckoutBranchAsync(long id, string branch, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Patch, $"api/2.0/repos/{id}") { Content = JsonContent.Create(new { branch }) };
        using var resp = await _http.SendAsync(req, ct);
        using var doc = await ReadAsync(resp, ct);
        return Map(doc.RootElement);
    }

    /// <summary>Cria o Git folder apontando para o repositório (provider: gitHub, azureDevOpsServices…).</summary>
    public async Task<DatabricksRepo> CreateAsync(string url, string provider, string workspacePath, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/2.0/repos", new { url, provider, path = workspacePath }, ct);
        using var doc = await ReadAsync(resp, ct);
        return Map(doc.RootElement);
    }

    private static DatabricksRepo Map(JsonElement e)
    {
        string? S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return new DatabricksRepo(e.GetProperty("id").GetInt64(), S("path") ?? "", S("url"), S("provider"), S("branch"), S("head_commit_id"));
    }

    private async Task<JsonDocument> ReadAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (resp.IsSuccessStatusCode) return JsonDocument.Parse(text.Length == 0 ? "{}" : text);
        string? code = null, message = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error_code", out var c)) code = c.GetString();
            if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
        }
        catch (JsonException) { }
        message = (message ?? (text.Length > 300 ? text[..300] : text)).Replace(_token, "***");
        throw new DatabricksException($"Databricks Repos HTTP {(int)resp.StatusCode}{(code != null ? $" {code}" : "")}: {message}", code, null, (int)resp.StatusCode);
    }

    public void Dispose() => _http.Dispose();
}
