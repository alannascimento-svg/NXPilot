// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text.RegularExpressions;

namespace NXPilot.Risk;

/// <summary>
/// Atalhos de aprovação que VOCÊ liga numa tarefa: "aprovar semelhantes" (mesma assinatura de
/// comando) e "aprovar médio e baixo". Valem só enquanto a tarefa dura e NUNCA liberam ALTO —
/// risco alto (inclusive escrever fora do worktree) sempre volta para você.
/// </summary>
public sealed class TaskApprovalRules
{
    private readonly HashSet<string> _similar = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>MÉDIO passa sozinho nesta tarefa.</summary>
    public bool ApproveMedium { get; set; }

    /// <summary>Assinaturas liberadas ("aprovar semelhantes").</summary>
    public IReadOnlyCollection<string> SimilarSignatures => _similar;

    public bool IsEmpty => !ApproveMedium && _similar.Count == 0;

    public void AddSimilar(string action) => _similar.Add(Signature(action));

    public void Clear()
    {
        ApproveMedium = false;
        _similar.Clear();
    }

    /// <summary>Motivo da aprovação automática, ou nulo se a decisão volta para você.</summary>
    public string? Allows(string action, RiskLevel level)
    {
        if (level == RiskLevel.Alto) return null;
        if (ApproveMedium) return NxText.T("regra da tarefa: médio e baixo", "task rule: medium and low", "regla de la tarea: medio y bajo");
        return _similar.Contains(Signature(action))
            ? NxText.T("regra da tarefa: semelhante a um já aprovado", "task rule: similar to one already approved", "regla de la tarea: similar a uno ya aprobado")
            : null;
    }

    /// <summary>
    /// Assinatura de uma ação: a ferramenta + o começo de cada comando ("python -m pacote.modulo",
    /// "git status"). Edit/Write: a ferramenta + a pasta e a extensão do arquivo. Argumentos
    /// livres (textos, caminhos, código inline) ficam de fora — é isso que torna "semelhante".
    /// </summary>
    public static string Signature(string action)
    {
        var colon = action.IndexOf(':');
        if (colon <= 0) return action.Trim();
        var tool = action[..colon].Trim();
        var rest = action[(colon + 1)..].Trim();
        if (tool is "Bash" or "PowerShell" or "Shell")
        {
            var heads = Regex.Split(rest, @"\s*(?:;|&&|\|\||\|)\s*")
                .Where(p => p.Length > 0)
                .Select(CommandHead)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return $"{tool}: {string.Join(" ; ", heads)}";
        }
        // Ferramentas de arquivo: mesma pasta e mesmo tipo de arquivo.
        var path = rest.Trim('"', '\'');
        var dir = Path.GetDirectoryName(path) ?? "";
        return $"{tool}: {dir}{Path.DirectorySeparatorChar}*{Path.GetExtension(path)}";
    }

    /// <summary>Executável + subcomando/flag + o módulo de "-m" (nunca o código de "-c", nem textos entre aspas).</summary>
    private static string CommandHead(string command)
    {
        var tokens = Regex.Matches(command, "\"[^\"]*\"|'[^']*'|\\S+").Select(m => m.Value).ToList();
        if (tokens.Count == 0) return "";
        var head = new List<string> { tokens[0] };
        if (tokens.Count > 1 && !tokens[1].StartsWith('"') && !tokens[1].StartsWith('\''))
        {
            head.Add(tokens[1]);
            if (tokens[1] == "-m" && tokens.Count > 2) head.Add(tokens[2]);
        }
        return string.Join(' ', head);
    }
}
