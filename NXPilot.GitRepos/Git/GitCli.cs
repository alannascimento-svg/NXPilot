// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Text;

namespace NXPilot.GitRepos;

public sealed record GitResult(int ExitCode, string Output, string Error)
{
    public bool Ok => ExitCode == 0;
}

/// <summary>
/// Camada baixa: roda o git instalado na máquina. O token NUNCA vai na linha de comando
/// (visível na lista de processos) nem no .git/config: entra por GIT_CONFIG_COUNT/KEY/VALUE
/// (git ≥ 2.31) como cabeçalho Authorization limitado ao host do repositório, só neste processo.
/// Sem token, o git não pergunta nada (sem janela de login, sem travar esperando teclado).
/// </summary>
public static class GitCli
{
    public static string Executable { get; set; } = "git";

    public static async Task<GitResult> RunAsync(string? workDir, GitCredential? credential, string? hostRoot,
        IEnumerable<string> args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        if (workDir != null) psi.WorkingDirectory = workDir;
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in Environment(credential, hostRoot)) psi.Environment[k] = v;

        using var p = Process.Start(psi) ?? throw new GitRepoException("Não deu para iniciar o git. Ele está instalado e no PATH?");
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(entireProcessTree: true); } catch { } throw; }
        return new GitResult(p.ExitCode, Redact(await stdout, credential), Redact(await stderr, credential));
    }

    /// <summary>Roda e lança <see cref="GitRepoException"/> se o git sair com erro.</summary>
    public static async Task<string> RunOrThrowAsync(string? workDir, GitCredential? credential, string? hostRoot,
        IEnumerable<string> args, CancellationToken ct = default)
    {
        var list = args.ToList();
        var r = await RunAsync(workDir, credential, hostRoot, list, ct);
        if (!r.Ok)
            throw new GitRepoException($"git {list.FirstOrDefault()} falhou ({r.ExitCode}): {Hint(r.Error.Trim())}", r.ExitCode);
        return r.Output;
    }

    /// <summary>Variáveis do processo git (exposto para teste). Header só para <paramref name="hostRoot"/>.</summary>
    public static IReadOnlyList<(string Key, string Value)> Environment(GitCredential? credential, string? hostRoot)
    {
        var env = new List<(string, string)>
        {
            ("GIT_TERMINAL_PROMPT", "0"),   // nunca pede usuário/senha no console
            ("GCM_INTERACTIVE", "never"),   // Git Credential Manager sem janela
            ("GIT_ASKPASS", ""), ("SSH_ASKPASS", ""),
        };
        var config = new List<(string, string)>();
        if (credential is { Token.Length: > 0 } && hostRoot != null)
        {
            config.Add(("credential.helper", "")); // não usa nem grava no gerenciador de credenciais
            config.Add(($"http.{hostRoot}.extraHeader", "Authorization: Basic " + BasicValue(credential)));
        }
        env.Add(("GIT_CONFIG_COUNT", config.Count.ToString()));
        for (var i = 0; i < config.Count; i++)
        {
            env.Add(($"GIT_CONFIG_KEY_{i}", config[i].Item1));
            env.Add(($"GIT_CONFIG_VALUE_{i}", config[i].Item2));
        }
        return env;
    }

    /// <summary>GitHub: x-access-token:&lt;token&gt;; Azure DevOps: :&lt;PAT&gt; (usuário vazio).</summary>
    private static string BasicValue(GitCredential c) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        (c.Provider == GitProvider.GitHub ? "x-access-token:" : ":") + c.Token));

    /// <summary>Tira o token (puro e em Base64) de qualquer texto que vá para log ou exceção.</summary>
    public static string Redact(string text, GitCredential? credential)
    {
        if (credential is not { Token.Length: > 0 } || text.Length == 0) return text;
        return text.Replace(credential.Token, "***").Replace(BasicValue(credential), "***");
    }

    private static string Hint(string error) =>
        error.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase) || error.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)
            ? error + " — token ausente, inválido ou sem permissão no repositório."
            : error.Length > 600 ? error[..600] + "…" : error;
}
