// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text.RegularExpressions;

namespace NXPilot.Risk;

/// <summary>
/// Scripts que um comando vai executar ("python gerar.py", "python -m pacote.modulo",
/// "pwsh -File x.ps1", "node app.js"…) e que existem no disco — para você LER antes de aprovar.
/// O comando em si diz pouco quando o risco está dentro do arquivo.
/// </summary>
public static class ScriptReferences
{
    private static readonly string[] Extensions = [".py", ".ps1", ".psm1", ".js", ".mjs", ".cjs", ".ts", ".sh", ".bat", ".cmd", ".rb", ".pl", ".sql"];

    /// <summary>Arquivos de script citados em <paramref name="action"/>, resolvidos contra <paramref name="workingDir"/>.</summary>
    public static IReadOnlyList<string> Find(string action, string? workingDir)
    {
        var colon = action.IndexOf(':');
        var command = colon > 0 ? action[(colon + 1)..] : action;
        var tokens = Regex.Matches(command, "\"[^\"]*\"|'[^']*'|[^\\s;|&]+").Select(m => m.Value.Trim('"', '\'')).ToList();
        var found = new List<string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (Extensions.Any(e => t.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                Add(found, Resolve(t, workingDir));
            else if (t == "-m" && i + 1 < tokens.Count && Regex.IsMatch(tokens[i + 1], @"^[A-Za-z_][\w.]*$"))
            {
                // python -m pacote.modulo → pacote\modulo.py ou pacote\modulo\__main__.py
                var rel = tokens[i + 1].Replace('.', Path.DirectorySeparatorChar);
                Add(found, Resolve(rel + ".py", workingDir) ?? Resolve(Path.Combine(rel, "__main__.py"), workingDir));
            }
        }
        return found;
    }

    private static string? Resolve(string path, string? workingDir)
    {
        try
        {
            var full = Path.IsPathRooted(path) ? path : workingDir is { Length: > 0 } ? Path.Combine(workingDir, path) : null;
            return full != null && File.Exists(full) ? Path.GetFullPath(full) : null;
        }
        catch { return null; } // caminho inválido no meio do comando: só não mostra
    }

    private static void Add(List<string> list, string? path)
    {
        if (path != null && !list.Contains(path, StringComparer.OrdinalIgnoreCase)) list.Add(path);
    }
}
