// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.GitRepos;

public enum GitProvider { GitHub, AzureDevOps, Other }

/// <summary>
/// Repositório remoto. <see cref="Parse"/> entende as URLs de clone/web do GitHub
/// (github.com/dono/repo) e do Azure DevOps (dev.azure.com/org/projeto/_git/repo e
/// org.visualstudio.com/projeto/_git/repo).
/// </summary>
public sealed record GitRepository(GitProvider Provider, string CloneUrl, string Owner, string Name,
    string? Project = null, string? DefaultBranch = null, string? WebUrl = null)
{
    /// <summary>"https://host/" — usado para limitar o cabeçalho de autenticação a esse host.</summary>
    public string HostRoot => Uri.TryCreate(CloneUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        ? u.GetLeftPart(UriPartial.Authority) + "/"
        : CloneUrl; // pasta local: não há host (e nenhum token é enviado)

    /// <summary>Organização do Azure DevOps como URL (https://dev.azure.com/org ou https://org.visualstudio.com).</summary>
    public string? AzureOrganizationUrl => Provider != GitProvider.AzureDevOps ? null
        : new Uri(CloneUrl).Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase)
            ? new Uri(CloneUrl).GetLeftPart(UriPartial.Authority)
            : $"https://dev.azure.com/{Owner}";

    public override string ToString() => Project == null ? $"{Owner}/{Name}" : $"{Owner}/{Project}/{Name}";

    public static GitRepository Parse(string url)
    {
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Use a URL https do repositório (GitHub ou Azure DevOps).", nameof(url));
        // Usuário embutido na URL (https://org@dev.azure.com/...) não é guardado — o token vai à parte.
        var clean = new UriBuilder(uri) { UserName = "", Password = "" }.Uri;
        var parts = clean.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var host = clean.Host.ToLowerInvariant();

        if (host == "github.com" && parts.Length >= 2)
        {
            var name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
            return new(GitProvider.GitHub, $"https://github.com/{parts[0]}/{name}.git", parts[0], name, WebUrl: $"https://github.com/{parts[0]}/{name}");
        }

        var git = Array.FindIndex(parts, p => p.Equals("_git", StringComparison.OrdinalIgnoreCase));
        if (git > 0 && git + 1 < parts.Length && (host == "dev.azure.com" || host.EndsWith(".visualstudio.com")))
        {
            var org = host == "dev.azure.com" ? parts[0] : host.Split('.')[0];
            var project = Uri.UnescapeDataString(parts[git - 1]);
            var repo = Uri.UnescapeDataString(parts[git + 1]);
            var root = clean.GetLeftPart(UriPartial.Authority) + "/" + string.Join('/', parts.Take(git + 2));
            return new(GitProvider.AzureDevOps, root, org, repo, project, WebUrl: root);
        }

        var last = parts.Length > 0 ? parts[^1] : host;
        return new(GitProvider.Other, clean.ToString(), parts.Length > 1 ? parts[^2] : host, last.EndsWith(".git") ? last[..^4] : last);
    }
}

/// <summary>Token de um provedor. Só em memória; o ToString nunca o mostra.</summary>
public sealed record GitCredential(GitProvider Provider, string Token)
{
    public override string ToString() => $"{Provider} (token {(Token.Length > 0 ? "definido" : "vazio")})";
}

/// <summary>De onde vem o token de cada provedor (no NXPilot: arquivo com DPAPI).</summary>
public interface IGitCredentialProvider
{
    GitCredential? For(GitProvider provider);
}

public sealed class StaticGitCredentials(string? gitHubToken = null, string? azureDevOpsToken = null) : IGitCredentialProvider
{
    public GitCredential? For(GitProvider provider) => provider switch
    {
        GitProvider.GitHub when !string.IsNullOrEmpty(gitHubToken) => new(provider, gitHubToken),
        GitProvider.AzureDevOps when !string.IsNullOrEmpty(azureDevOpsToken) => new(provider, azureDevOpsToken),
        _ => null,
    };
}

public sealed record GitFileChange(string Status, string Path);

public sealed record PullRequestInfo(string Id, string Title, string Url, string SourceBranch, string TargetBranch);

/// <summary>Erro do git ou da API. A mensagem já vem sem o token.</summary>
public class GitRepoException(string message, int? exitCode = null, int? httpStatus = null) : Exception(message)
{
    public int? ExitCode { get; } = exitCode;
    public int? HttpStatus { get; } = httpStatus;
}

public sealed class GitOperationDeniedException(GitOperationReview review, string reason)
    : Exception($"Operação git não aprovada ({review.Kind}): {reason}")
{
    public GitOperationReview Review { get; } = review;
}
