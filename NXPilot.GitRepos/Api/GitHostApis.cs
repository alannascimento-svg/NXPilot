// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NXPilot.GitRepos;

/// <summary>REST do provedor — só o que o git não faz: listar repositórios e abrir pull request.</summary>
public interface IGitHostApi : IDisposable
{
    GitProvider Provider { get; }
    Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(CancellationToken ct = default);
    Task<PullRequestInfo> CreatePullRequestAsync(GitRepository repo, string sourceBranch, string targetBranch,
        string title, string description, CancellationToken ct = default);
}

/// <summary>GitHub REST (api.github.com, versão 2022-11-28). Token: fine-grained ou clássico com "repo".</summary>
public sealed class GitHubApi : IGitHostApi
{
    private readonly HttpClient _http;
    private readonly string _token;
    public GitProvider Provider => GitProvider.GitHub;

    public GitHubApi(string token, HttpMessageHandler? handler = null, string apiBase = "https://api.github.com/")
    {
        _token = token;
        _http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NXPilot.GitRepos", "1.0")); // obrigatório no GitHub
    }

    /// <summary>Repositórios que o token enxerga (próprios, de organizações e colaborações), até 1000.</summary>
    public async Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(CancellationToken ct = default)
    {
        var list = new List<GitRepository>();
        for (var page = 1; page <= 10; page++)
        {
            using var doc = await GetJsonAsync($"user/repos?per_page=100&page={page}&sort=pushed", ct);
            var items = doc.RootElement.EnumerateArray().ToList();
            foreach (var r in items)
                list.Add(new GitRepository(GitProvider.GitHub, r.GetProperty("clone_url").GetString()!,
                    r.GetProperty("owner").GetProperty("login").GetString()!, r.GetProperty("name").GetString()!,
                    DefaultBranch: r.TryGetProperty("default_branch", out var b) ? b.GetString() : null,
                    WebUrl: r.GetProperty("html_url").GetString()));
            if (items.Count < 100) break;
        }
        return list;
    }

    /// <summary>Cria um repositório VAZIO na conta do token (privado por padrão) — destino de "mover para o GitHub".</summary>
    public async Task<GitRepository> CreateRepositoryAsync(string name, string description, bool isPrivate = true, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("user/repos", new { name, description, @private = isPrivate, auto_init = false }, ct);
        using var doc = await ReadAsync(resp, ct);
        var r = doc.RootElement;
        return new GitRepository(GitProvider.GitHub, r.GetProperty("clone_url").GetString()!,
            r.GetProperty("owner").GetProperty("login").GetString()!, r.GetProperty("name").GetString()!,
            DefaultBranch: "main", WebUrl: r.GetProperty("html_url").GetString());
    }

    public async Task<PullRequestInfo> CreatePullRequestAsync(GitRepository repo, string sourceBranch, string targetBranch,
        string title, string description, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync($"repos/{Uri.EscapeDataString(repo.Owner)}/{Uri.EscapeDataString(repo.Name)}/pulls",
            new { title, head = sourceBranch, @base = targetBranch, body = description }, ct);
        using var doc = await ReadAsync(resp, ct);
        return new PullRequestInfo(doc.RootElement.GetProperty("number").GetInt32().ToString(), title,
            doc.RootElement.GetProperty("html_url").GetString()!, sourceBranch, targetBranch);
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(path, ct);
        return await ReadAsync(resp, ct);
    }

    private async Task<JsonDocument> ReadAsync(HttpResponseMessage resp, CancellationToken ct) =>
        await HttpErrors.ReadJsonAsync(resp, "GitHub", _token, ct);

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Azure DevOps Repos REST (api-version 7.1). PAT com "Code (Read &amp; Write)". A organização
/// é https://dev.azure.com/org (ou https://org.visualstudio.com).
/// </summary>
public sealed class AzureReposApi : IGitHostApi
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly string _org;
    public GitProvider Provider => GitProvider.AzureDevOps;

    public AzureReposApi(string organizationUrl, string pat, HttpMessageHandler? handler = null)
    {
        _org = organizationUrl.Trim().TrimEnd('/');
        _token = pat;
        _http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + pat)));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Todos os repositórios da organização (de todos os projetos que o PAT enxerga).</summary>
    public async Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"{_org}/_apis/git/repositories?api-version=7.1", ct);
        using var doc = await HttpErrors.ReadJsonAsync(resp, "Azure DevOps", _token, ct);
        return doc.RootElement.GetProperty("value").EnumerateArray()
            .Where(r => !(r.TryGetProperty("isDisabled", out var d) && d.GetBoolean()))
            .Select(r => GitRepository.Parse(r.GetProperty("remoteUrl").GetString()!) with
            {
                DefaultBranch = r.TryGetProperty("defaultBranch", out var b) ? b.GetString()?.Replace("refs/heads/", "") : null,
                WebUrl = r.TryGetProperty("webUrl", out var w) ? w.GetString() : null,
            })
            .ToList();
    }

    public async Task<PullRequestInfo> CreatePullRequestAsync(GitRepository repo, string sourceBranch, string targetBranch,
        string title, string description, CancellationToken ct = default)
    {
        var url = $"{_org}/{Uri.EscapeDataString(repo.Project ?? "")}/_apis/git/repositories/{Uri.EscapeDataString(repo.Name)}/pullrequests?api-version=7.1";
        using var resp = await _http.PostAsJsonAsync(url, new
        {
            sourceRefName = "refs/heads/" + sourceBranch,
            targetRefName = "refs/heads/" + targetBranch,
            title, description,
        }, ct);
        using var doc = await HttpErrors.ReadJsonAsync(resp, "Azure DevOps", _token, ct);
        var id = doc.RootElement.GetProperty("pullRequestId").GetInt32().ToString();
        return new PullRequestInfo(id, title, $"{repo.CloneUrl}/pullrequest/{id}", sourceBranch, targetBranch);
    }

    public void Dispose() => _http.Dispose();
}

internal static class HttpErrors
{
    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage resp, string provider, string token, CancellationToken ct)
    {
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (resp.IsSuccessStatusCode) return JsonDocument.Parse(text);
        string? message = null;
        try { using var doc = JsonDocument.Parse(text); if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString(); }
        catch (JsonException) { }
        var status = (int)resp.StatusCode;
        var hint = status switch
        {
            401 => provider == "Azure DevOps" ? " PAT inválido, expirado ou sem o escopo \"Code (Read & Write)\"." : " Token inválido ou expirado.",
            403 => " Sem permissão.",
            404 => " Não encontrado (ou sem acesso).",
            _ => ""
        };
        message = message ?? (text.Length > 300 ? text[..300] : text);
        if (token.Length > 0) message = message.Replace(token, "***");
        throw new GitRepoException($"{provider} HTTP {status}: {message}{hint}", httpStatus: status);
    }
}
