// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NXPilot.Risk;

/// <summary>
/// Regras determinísticas: instantâneas e auditáveis, mas só reconhecem o que foi escrito
/// nelas. <see cref="TryMatch"/> devolve nulo quando nenhuma regra casa — é aí que o
/// classificador híbrido passa a vez ao modelo.
/// </summary>
public sealed class RuleRiskClassifier : IRiskClassifier
{
    // Motivo nos dois idiomas: a lista é estática, o idioma é escolhido na hora de exibir.
    private sealed record Rule(RiskLevel Level, Regex Pattern, string ReasonPt, string ReasonEn)
    {
        public string Reason => NxText.T(ReasonPt, ReasonEn);
    }

    private static Rule R(RiskLevel level, string pattern, string reasonPt, string reasonEn) =>
        new(level, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled), reasonPt, reasonEn);

    // Ordem importa: a primeira regra que casa vence, por isso Alto vem antes de Médio/Baixo.
    private static readonly Rule[] Rules =
    {
        R(RiskLevel.Alto, @"\brm\s+-[a-z]*r[a-z]*f|\brm\s+-[a-z]*f[a-z]*r", "remoção recursiva forçada", "forced recursive delete"),
        R(RiskLevel.Alto, @"Remove-Item\b.*-Recurse|\b(del|rd|rmdir)\s+/s", "remoção recursiva (Windows)", "recursive delete (Windows)"),
        R(RiskLevel.Alto, @"git\s+push\b.*(--force|\s-f\b)", "push forçado reescreve o remoto", "forced push rewrites the remote"),
        R(RiskLevel.Alto, @"git\s+reset\s+--hard|git\s+clean\s+-[a-z]*f", "descarta trabalho local", "discards local work"),
        R(RiskLevel.Alto, @"\b(drop\s+(table|database)|truncate\s+table)\b", "destrói dados no banco", "destroys database data"),
        R(RiskLevel.Alto, @"(curl|wget|iwr|Invoke-WebRequest)\b.*\|\s*(sh|bash|iex|Invoke-Expression)", "executa script baixado", "runs a downloaded script"),
        R(RiskLevel.Alto, @"\bsudo\b|\bchmod\s+777\b|\bshutdown\b|\breg\s+delete\b", "privilégio/sistema", "privilege/system"),
        R(RiskLevel.Alto, @"\.env\b|secrets?\.(json|ya?ml)|id_rsa", "toca segredos", "touches secrets"),
        R(RiskLevel.Alto, @"\bnpm\s+publish\b|\bdotnet\s+nuget\s+push\b", "publica pacote", "publishes a package"),

        R(RiskLevel.Medio, @"\b(npm|pnpm|yarn)\s+(install|add|i)\b|\bpip\s+install\b|\bdotnet\s+add\b", "instala dependência", "installs a dependency"),
        R(RiskLevel.Medio, @"git\s+(commit|push|merge|rebase)\b", "altera histórico/remoto", "changes history/remote"),
        R(RiskLevel.Medio, @"^(Edit|Write|MultiEdit)\s*:", "escreve arquivo no worktree", "writes a file in the worktree"),
        R(RiskLevel.Medio, @"^Delete\s*:", "remove arquivo do worktree", "deletes a file in the worktree"),
        R(RiskLevel.Medio, @"\b(mkdir|New-Item|mv|Move-Item|cp|Copy-Item)\b", "altera arquivos", "changes files"),
        // Rodar script/interpretador executa código arbitrário: nunca é "só leitura".
        R(RiskLevel.Medio, @"^(Bash|PowerShell):\s*(pwsh|powershell|bash|sh|python3?|node|ruby|perl|\.[\\/])", "executa script/programa", "runs a script/program"),

        R(RiskLevel.Baixo, @"^(Read|Glob|Grep|WebFetch|WebSearch)\s*:", "somente leitura", "read-only"),
        R(RiskLevel.Baixo, @"git\s+(status|diff|log|show|blame|ls-files|rev-parse)\b", "git somente leitura", "read-only git"),
        R(RiskLevel.Baixo, @"^(Bash|PowerShell):\s*(ls|dir|cat|pwd|echo|type|cd|Set-Location|Get-Content|Get-ChildItem|Get-Location|rg|grep)\b", "comando de leitura", "read command"),
        R(RiskLevel.Baixo, @"^(Bash|PowerShell):\s*(head|tail|wc|sort|uniq|od|cut|tr|less|more|findstr|which|where|stat|file|tree|Select-Object|Select-String|Measure-Object|Format-\w+|Out-String)\b", "filtro de leitura", "read filter"),
        R(RiskLevel.Baixo, @"\b(dotnet\s+(build|test)|npm\s+(test|run\s+(lint|test|build)))\b", "build/teste", "build/test"),
    };

    // Separadores de comando composto (;  &&  ||  |  quebra de linha).
    private static readonly Regex Separators = new(@"\s*(?:;|&&|\|\||\||\r?\n)\s*", RegexOptions.Compiled);
    private static readonly Regex ShellPrefix = new(@"^(Bash|PowerShell):\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Name => NxText.T("Regras", "Rules");

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Nulo quando nenhuma regra reconhece a ação.
    ///
    /// Comando composto (a; b && c | d): primeiro procura ALTO no texto inteiro (pega
    /// "curl … | bash"); depois cada parte vale por si e o resultado é a PIOR delas. Uma parte
    /// desconhecida torna o todo MÉDIO — um `git status` no fim não pode liberar o resto.
    /// </summary>
    public RiskResult? TryMatch(string action)
    {
        var sw = Stopwatch.StartNew();
        if (MatchFirst(action, onlyHigh: true) is { } high)
            return new RiskResult(RiskLevel.Alto, 1.0, Name, high.Reason, sw.Elapsed);

        var prefix = ShellPrefix.Match(action);
        if (prefix.Success)
        {
            var parts = Separators.Split(action[prefix.Length..]).Where(p => p.Length > 0).ToList();
            if (parts.Count > 1)
            {
                var matched = parts.Select(p => MatchFirst(prefix.Value + p, onlyHigh: false)).ToList();
                if (matched.Any(m => m == null))
                    return new RiskResult(RiskLevel.Medio, 0.5, Name,
                        NxText.T("comando composto com parte não reconhecida", "compound command with an unrecognized part"), sw.Elapsed);
                var worst = matched.MaxBy(m => m!.Level)!;
                return new RiskResult(worst.Level, 1.0, Name,
                    NxText.T("comando composto: ", "compound command: ") + string.Join(" + ", matched.Select(m => m!.Reason).Distinct()), sw.Elapsed);
            }
        }

        return MatchFirst(action, onlyHigh: false) is { } rule
            ? new RiskResult(rule.Level, 1.0, Name, rule.Reason, sw.Elapsed)
            : null;
    }

    private static Rule? MatchFirst(string text, bool onlyHigh) =>
        Rules.FirstOrDefault(r => (!onlyHigh || r.Level == RiskLevel.Alto) && r.Pattern.IsMatch(text));

    /// <summary>Sem regra, chuta Médio com confiança zero (nunca aprova sozinho).</summary>
    public Task<RiskResult> ClassifyAsync(string action, CancellationToken ct = default) =>
        Task.FromResult(TryMatch(action) ?? new RiskResult(RiskLevel.Medio, 0, Name, NxText.T("nenhuma regra casou", "no rule matched"), TimeSpan.Zero));

    public void Dispose() { }
}

/// <summary>Regras primeiro; o que elas não reconhecem vai para o modelo (ONNX ou LLM).</summary>
public sealed class HybridRiskClassifier(RuleRiskClassifier rules, IRiskClassifier model) : IRiskClassifier
{
    public string Name => $"{NxText.T("Regras", "Rules")} + {model.Name}";

    public Task InitializeAsync(CancellationToken ct = default) => model.InitializeAsync(ct);

    public async Task<RiskResult> ClassifyAsync(string action, CancellationToken ct = default)
    {
        var hit = rules.TryMatch(action);
        if (hit != null) return hit with { Engine = Name };
        var r = await model.ClassifyAsync(action, ct);
        return r with { Engine = Name };
    }

    public void Dispose() => model.Dispose();
}

/// <summary>
/// Cascata: regras (µs) → modelo rápido (ms) → modelo lento (s) só quando o rápido tem
/// confiança abaixo de <paramref name="threshold"/>. Paga o LLM apenas nos casos difíceis.
/// </summary>
public sealed class CascadeRiskClassifier(RuleRiskClassifier rules, IRiskClassifier fast, IRiskClassifier slow, double threshold = 0.6)
    : IRiskClassifier
{
    public string Name => $"{NxText.T("Regras", "Rules")} → {fast.Name} → {Regex.Replace(slow.Name, @"\s*\(.*\)$", "")}";

    /// <summary>Quantas classificações precisaram do modelo lento.</summary>
    public int SlowCalls { get; private set; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await fast.InitializeAsync(ct);
        await slow.InitializeAsync(ct);
    }

    public async Task<RiskResult> ClassifyAsync(string action, CancellationToken ct = default)
    {
        var hit = rules.TryMatch(action);
        if (hit != null) return hit with { Engine = Name };

        var quick = await fast.ClassifyAsync(action, ct);
        if (quick.Confidence >= threshold) return quick with { Engine = Name };

        SlowCalls++;
        var careful = await slow.ClassifyAsync(action, ct);
        return careful with { Engine = Name, Elapsed = quick.Elapsed + careful.Elapsed,
                              Reason = NxText.T($"ONNX em dúvida ({quick.Confidence:P0}) → {careful.Reason}", $"ONNX unsure ({quick.Confidence:P0}) → {careful.Reason}") };
    }

    public void Dispose()
    {
        fast.Dispose();
        slow.Dispose();
    }
}
