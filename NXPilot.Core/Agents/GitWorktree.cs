// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Text;

namespace NXPilot.Agents;

/// <summary>
/// Um git worktree por tarefa: o agente trabalha numa cópia isolada, num branch próprio
/// (nxpilot/&lt;tarefa&gt;), e nunca mexe na pasta em que você está trabalhando. Juntar o
/// resultado (merge/PR) continua sendo decisão sua.
/// </summary>
public static class GitWorktree
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "worktrees");

    /// <summary>Valida o repositório e cria o worktree; devolve (pasta, branch).</summary>
    public static async Task<(string Path, string Branch)> CreateAsync(string repoPath, string taskId, CancellationToken ct = default)
    {
        var top = (await GitAsync(repoPath, ct, "rev-parse", "--show-toplevel")).Trim();
        if (top.Length == 0)
            throw new InvalidOperationException(NxText.T($"{repoPath} não é um repositório git.", $"{repoPath} is not a git repository.", $"{repoPath} no es un repositorio git."));
        if ((await GitAsync(top, ct, "rev-parse", "--verify", "HEAD")).Trim().Length == 0)
            throw new InvalidOperationException(NxText.T("O repositório não tem nenhum commit ainda.", "The repository has no commits yet.", "El repositorio aún no tiene ningún commit."));

        var name = new DirectoryInfo(top).Name;
        var path = Path.Combine(Root, $"{name}-{taskId}");
        var branch = $"nxpilot/{taskId}";
        Directory.CreateDirectory(Root);
        await GitAsync(top, ct, "worktree", "add", "-b", branch, path, "HEAD");
        if (!Directory.Exists(path))
            throw new InvalidOperationException(NxText.T("Não foi possível criar o worktree.", "Could not create the worktree.", "No fue posible crear el worktree."));
        return (path, branch);
    }

    /// <summary>Commit de onde o worktree nasceu — base do diff da tarefa inteira.</summary>
    public static async Task<string> HeadAsync(string worktree, CancellationToken ct = default) =>
        (await GitAsync(worktree, ct, "rev-parse", "HEAD")).Trim();

    /// <summary>
    /// Diff do trabalho do agente desde <paramref name="baseCommit"/> (commits das fases + o que
    /// ainda não foi commitado), incluindo arquivos novos (marcados com intent-to-add).
    /// </summary>
    public static async Task<string> DiffAsync(string worktree, string? baseCommit = null, CancellationToken ct = default)
    {
        await GitAsync(worktree, ct, "add", "-A", "--intent-to-add");
        return baseCommit is { Length: > 0 }
            ? await GitAsync(worktree, ct, "diff", "--no-color", baseCommit)
            : await GitAsync(worktree, ct, "diff", "--no-color");
    }

    /// <summary>
    /// Checkpoint ao fim de cada fase, feito pelo NXPilot (não pelo agente): cada fase vira
    /// um commit no branch da tarefa. Devolve false quando a fase não mudou nada.
    /// </summary>
    public static async Task<bool> CommitAllAsync(string worktree, string message, CancellationToken ct = default)
    {
        await GitAsync(worktree, ct, "add", "-A");
        if ((await GitAsync(worktree, ct, "status", "--porcelain")).Trim().Length == 0) return false;
        await GitAsync(worktree, ct, "-c", "user.name=NXPilot", "-c", "user.email=nxpilot@local", "commit", "-q", "-m", message);
        return true;
    }

    /// <summary>Apaga o worktree e o branch da tarefa (Histórico → remover). Irreversível.</summary>
    public static async Task RemoveAsync(string repoPath, string worktree, string? branch, CancellationToken ct = default)
    {
        await GitAsync(repoPath, ct, "worktree", "remove", "--force", worktree);
        if (!string.IsNullOrEmpty(branch)) await GitAsync(repoPath, ct, "branch", "-D", branch);
    }

    private static async Task<string> GitAsync(string cwd, CancellationToken ct, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("git");
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        var error = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        // rev-parse em repositório inválido sai com erro: devolve vazio e quem chamou decide.
        if (p.ExitCode != 0 && args[0] is not "rev-parse")
            throw new InvalidOperationException($"git {string.Join(' ', args)}: {error.Trim()}");
        return output;
    }
}
