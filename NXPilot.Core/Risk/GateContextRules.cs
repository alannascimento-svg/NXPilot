// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using NXPilot.Agents;

namespace NXPilot.Risk;

public enum GateContextDecision
{
    /// <summary>Nenhuma regra de contexto se aplica: vale a cascata.</summary>
    None,
    /// <summary>Arquivo de plano do próprio Claude (~/.claude/plans): liberado.</summary>
    AllowPlanFile,
    /// <summary>Escrita FORA do worktree da tarefa: sempre ALTO, independe da cascata.</summary>
    ForceHigh,
}

/// <summary>
/// Regras de contexto aplicadas ANTES da cascata a todo pedido de agente real. São regras de
/// segurança (o agente não sai da caixa sem você), por isso ficam isoladas e testadas.
/// </summary>
public static class GateContextRules
{
    private static readonly string[] WriteTools = { "Write", "Edit", "MultiEdit", "NotebookEdit", "Delete" };

    public static string DefaultPlansFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "plans");

    /// <summary>Caminho do arquivo que a ferramenta vai escrever; nulo se não for escrita.</summary>
    public static string? TargetPath(GateRequest req) =>
        WriteTools.Contains(req.Tool) && req.Action.IndexOf(':') is var i and >= 0 ? req.Action[(i + 1)..].Trim() : null;

    public static GateContextDecision Evaluate(GateRequest req, string? worktree, string? plansFolder = null)
    {
        if (TargetPath(req) is not { Length: > 0 } path) return GateContextDecision.None;
        var full = Path.GetFullPath(path);
        if (IsUnder(full, plansFolder ?? DefaultPlansFolder)) return GateContextDecision.AllowPlanFile;
        if (worktree != null && !IsUnder(full, worktree)) return GateContextDecision.ForceHigh;
        return GateContextDecision.None;
    }

    /// <summary>
    /// "Está dentro da pasta" de verdade: compara pasta inteira (C:\wt\a não está dentro de
    /// C:\wt\abc) e ignora maiúsculas, como o Windows.
    /// </summary>
    public static bool IsUnder(string fullPath, string folder)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(fullPath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
