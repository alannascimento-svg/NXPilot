// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.GitRepos;

/// <summary>Do mais inofensivo ao que sai da máquina.</summary>
public enum GitOperationKind
{
    Clone,        // baixa para uma pasta nova
    Pull,         // atualiza a cópia local (só fast-forward)
    Commit,       // grava localmente — dá para desfazer
    Push,         // envia ao servidor (nunca --force)
    PullRequest,  // abre PR no GitHub/DevOps
    Deploy,       // leva o código a outro sistema (ex.: Git folder do Databricks)
}

/// <summary>O que vai acontecer — é isto que vai para a aprovação (gate do NXPilot).</summary>
public sealed record GitOperationReview(GitOperationKind Kind, string Repository, string? Branch, bool TargetsDefaultBranch, string Detail)
{
    public bool LeavesMachine => Kind >= GitOperationKind.Push;
    public string Summary => $"Git {Kind}: {Repository}{(Branch != null ? $" [{Branch}{(TargetsDefaultBranch ? ", branch principal" : "")}]" : "")} — {Detail}";
}

public interface IGitOperationApprover
{
    Task<GitApproval> ApproveAsync(GitOperationReview review, CancellationToken ct = default);
}

public sealed record GitApproval(bool Approved, string Reason)
{
    public static GitApproval Allow(string reason) => new(true, reason);
    public static GitApproval Deny(string reason) => new(false, reason);
}

/// <summary>Padrão seguro do pacote: clone, pull e commit local passam; nada sai da máquina sem aprovador.</summary>
public sealed class LocalOnlyGitApprover : IGitOperationApprover
{
    public Task<GitApproval> ApproveAsync(GitOperationReview review, CancellationToken ct = default) =>
        Task.FromResult(review.LeavesMachine
            ? GitApproval.Deny("push, pull request e deploy precisam de um aprovador")
            : GitApproval.Allow("operação local"));
}

/// <summary>Camada de serviço: o que o app (e os agentes) usam para GitHub e Azure DevOps Repos.</summary>
public interface IGitRepoService
{
    Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(GitProvider provider, string? azureOrganizationUrl = null, CancellationToken ct = default);

    /// <summary>Clona em <paramref name="targetDir"/>; se a pasta já for o mesmo repositório, faz pull.</summary>
    Task<string> CloneOrPullAsync(GitRepository repo, string targetDir, string? branch = null, CancellationToken ct = default);
    Task PullAsync(string repoDir, CancellationToken ct = default);
    Task<IReadOnlyList<GitFileChange>> StatusAsync(string repoDir, CancellationToken ct = default);
    Task<string> CurrentBranchAsync(string repoDir, CancellationToken ct = default);
    Task CreateBranchAsync(string repoDir, string branch, CancellationToken ct = default);

    /// <summary>git add -A + commit. Devolve o SHA, ou null se não havia nada para commitar.</summary>
    Task<string?> CommitAsync(string repoDir, string message, string? authorName = null, string? authorEmail = null, CancellationToken ct = default);

    /// <summary>Envia a branch atual (ou <paramref name="branch"/>) para o origin, com upstream. Nunca força.</summary>
    Task PushAsync(string repoDir, string? branch = null, CancellationToken ct = default);

    Task<PullRequestInfo> CreatePullRequestAsync(string repoDir, string title, string description, string? targetBranch = null, CancellationToken ct = default);
}

public sealed class GitRepoService(
    IGitCredentialProvider credentials,
    IGitOperationApprover? approver = null,
    Func<GitProvider, GitCredential, string?, IGitHostApi>? apiFactory = null,
    Action<string>? log = null) : IGitRepoService
{
    private readonly IGitOperationApprover _approver = approver ?? new LocalOnlyGitApprover();
    private readonly Func<GitProvider, GitCredential, string?, IGitHostApi> _api = apiFactory ?? DefaultApi;

    private static IGitHostApi DefaultApi(GitProvider provider, GitCredential credential, string? azureOrg) => provider switch
    {
        GitProvider.GitHub => new GitHubApi(credential.Token),
        GitProvider.AzureDevOps => new AzureReposApi(azureOrg ?? throw new ArgumentException("Informe a organização do Azure DevOps (https://dev.azure.com/org)."), credential.Token),
        _ => throw new NotSupportedException("Só GitHub e Azure DevOps têm API aqui."),
    };

    public async Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(GitProvider provider, string? azureOrganizationUrl = null, CancellationToken ct = default)
    {
        using var api = _api(provider, Credential(provider), azureOrganizationUrl);
        return await api.ListRepositoriesAsync(ct);
    }

    public async Task<string> CloneOrPullAsync(GitRepository repo, string targetDir, string? branch = null, CancellationToken ct = default)
    {
        targetDir = Path.GetFullPath(targetDir);
        if (Directory.Exists(Path.Combine(targetDir, ".git")))
        {
            var origin = await RemoteAsync(targetDir, ct);
            if (!SameRepo(origin.CloneUrl, repo.CloneUrl))
                throw new GitRepoException($"A pasta {targetDir} já tem outro repositório ({origin}).");
            await PullAsync(targetDir, ct);
            return targetDir;
        }
        if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
            throw new GitRepoException($"A pasta {targetDir} existe e não está vazia.");

        await ApproveAsync(new GitOperationReview(GitOperationKind.Clone, repo.ToString(), branch, false, $"clonar em {targetDir}"), ct);
        var args = new List<string> { "clone", "--no-tags" };
        if (branch != null) args.AddRange(["--branch", branch]);
        args.AddRange(["--", repo.CloneUrl, targetDir]);
        await GitCli.RunOrThrowAsync(null, CredentialOrNull(repo.Provider), repo.HostRoot, args, ct);
        log?.Invoke($"Git: {repo} clonado em {targetDir}");
        return targetDir;
    }

    public async Task PullAsync(string repoDir, CancellationToken ct = default)
    {
        var repo = await RemoteAsync(repoDir, ct);
        await ApproveAsync(new GitOperationReview(GitOperationKind.Pull, repo.ToString(), await CurrentBranchAsync(repoDir, ct), false, "atualizar (fast-forward)"), ct);
        await Git(repoDir, repo, ct, "pull", "--ff-only", "--no-tags");
        log?.Invoke($"Git: {repo} atualizado");
    }

    public async Task<IReadOnlyList<GitFileChange>> StatusAsync(string repoDir, CancellationToken ct = default)
    {
        var output = await GitCli.RunOrThrowAsync(repoDir, null, null, ["status", "--porcelain=v1", "--untracked-files=all"], ct);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Length > 3)
            .Select(l => new GitFileChange(l[..2].Trim(), l[3..].Trim().Trim('"')))
            .ToList();
    }

    public async Task<string> CurrentBranchAsync(string repoDir, CancellationToken ct = default) =>
        (await GitCli.RunOrThrowAsync(repoDir, null, null, ["rev-parse", "--abbrev-ref", "HEAD"], ct)).Trim();

    public async Task CreateBranchAsync(string repoDir, string branch, CancellationToken ct = default)
    {
        var check = await GitCli.RunAsync(repoDir, null, null, ["check-ref-format", "--branch", branch], ct);
        if (!check.Ok) throw new GitRepoException($"Nome de branch inválido: {branch}");
        await GitCli.RunOrThrowAsync(repoDir, null, null, ["switch", "-c", branch], ct);
    }

    public async Task<string?> CommitAsync(string repoDir, string message, string? authorName = null, string? authorEmail = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Mensagem de commit vazia.", nameof(message));
        var changes = await StatusAsync(repoDir, ct);
        if (changes.Count == 0) return null;
        var repo = await RemoteAsync(repoDir, ct);
        var branch = await CurrentBranchAsync(repoDir, ct);
        await ApproveAsync(new GitOperationReview(GitOperationKind.Commit, repo.ToString(), branch, await IsDefaultBranchAsync(repoDir, branch, ct),
            $"{changes.Count} arquivo(s): {message.Split('\n')[0]}"), ct);

        await GitCli.RunOrThrowAsync(repoDir, null, null, ["add", "-A"], ct);
        var args = new List<string>();
        if (authorName != null && authorEmail != null) args.AddRange(["-c", $"user.name={authorName}", "-c", $"user.email={authorEmail}"]);
        args.AddRange(["commit", "-m", message]);
        await GitCli.RunOrThrowAsync(repoDir, null, null, args, ct);
        var sha = (await GitCli.RunOrThrowAsync(repoDir, null, null, ["rev-parse", "HEAD"], ct)).Trim();
        log?.Invoke($"Git: commit {sha[..Math.Min(8, sha.Length)]} em {branch} ({changes.Count} arquivo(s))");
        return sha;
    }

    public async Task PushAsync(string repoDir, string? branch = null, CancellationToken ct = default)
    {
        var repo = await RemoteAsync(repoDir, ct);
        branch ??= await CurrentBranchAsync(repoDir, ct);
        var isDefault = await IsDefaultBranchAsync(repoDir, branch, ct);
        var ahead = (await GitCli.RunAsync(repoDir, null, null, ["log", "--oneline", $"origin/{branch}..{branch}"], ct)) is { Ok: true } r
            ? $"{r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length} commit(s) novos"
            : "branch nova no servidor";
        await ApproveAsync(new GitOperationReview(GitOperationKind.Push, repo.ToString(), branch, isDefault, $"git push origin {branch} ({ahead})"), ct);
        await Git(repoDir, repo, ct, "push", "--set-upstream", "origin", branch);
        log?.Invoke($"Git: {branch} enviado para {repo}");
    }

    public async Task<PullRequestInfo> CreatePullRequestAsync(string repoDir, string title, string description, string? targetBranch = null, CancellationToken ct = default)
    {
        var repo = await RemoteAsync(repoDir, ct);
        var source = await CurrentBranchAsync(repoDir, ct);
        targetBranch ??= await DefaultBranchAsync(repoDir, ct) ?? "main";
        await ApproveAsync(new GitOperationReview(GitOperationKind.PullRequest, repo.ToString(), targetBranch, true, $"PR {source} → {targetBranch}: {title}"), ct);
        using var api = _api(repo.Provider, Credential(repo.Provider), repo.AzureOrganizationUrl);
        var pr = await api.CreatePullRequestAsync(repo, source, targetBranch, title, description, ct);
        log?.Invoke($"Git: PR {pr.Id} aberto — {pr.Url}");
        return pr;
    }

    // ------------------------------------------------------------ apoio

    private async Task ApproveAsync(GitOperationReview review, CancellationToken ct)
    {
        var a = await _approver.ApproveAsync(review, ct);
        log?.Invoke($"{(a.Approved ? "APROVADO" : "NEGADO")} ({a.Reason}) — {review.Summary}");
        if (!a.Approved) throw new GitOperationDeniedException(review, a.Reason);
    }

    private Task<string> Git(string repoDir, GitRepository repo, CancellationToken ct, params string[] args) =>
        GitCli.RunOrThrowAsync(repoDir, CredentialOrNull(repo.Provider), repo.HostRoot, args, ct);

    private GitCredential? CredentialOrNull(GitProvider provider) => credentials.For(provider);

    private GitCredential Credential(GitProvider provider) =>
        credentials.For(provider) ?? throw new GitRepoException($"Sem token para {provider}. Configure antes de usar a API.");

    private static async Task<GitRepository> RemoteAsync(string repoDir, CancellationToken ct)
    {
        var r = await GitCli.RunAsync(repoDir, null, null, ["remote", "get-url", "origin"], ct);
        if (!r.Ok) throw new GitRepoException($"{repoDir} não é um repositório git com 'origin'.");
        var url = r.Output.Trim();
        // Origin local (pasta) também vale — usado em testes e em espelhos internos.
        return Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            ? GitRepository.Parse(url)
            : new GitRepository(GitProvider.Other, url, "local", Path.GetFileName(url.TrimEnd('/', '\\')));
    }

    private static async Task<string?> DefaultBranchAsync(string repoDir, CancellationToken ct)
    {
        var r = await GitCli.RunAsync(repoDir, null, null, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], ct);
        return r.Ok ? r.Output.Trim().Replace("origin/", "") : null;
    }

    private static async Task<bool> IsDefaultBranchAsync(string repoDir, string branch, CancellationToken ct) =>
        await DefaultBranchAsync(repoDir, ct) is { } d ? d == branch : branch is "main" or "master";

    private static bool SameRepo(string a, string b) =>
        string.Equals(a.TrimEnd('/').Replace(".git", ""), b.TrimEnd('/').Replace(".git", ""), StringComparison.OrdinalIgnoreCase);
}
