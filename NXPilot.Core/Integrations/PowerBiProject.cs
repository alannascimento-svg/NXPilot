// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text.Json;
using NXPilot.Agents;

namespace NXPilot.Integrations;

/// <summary>
/// Projeto Power BI no formato PBIP (texto, versionável): o modelo semântico em TMDL
/// (&lt;nome&gt;.SemanticModel/definition/*.tmdl) e o relatório em PBIR
/// (&lt;nome&gt;.Report/definition/**/*.json). O agente edita esses arquivos como código — sem
/// clicar em nada e sem biblioteca da Microsoft (decisão: nada com EULA de indenização).
/// </summary>
public sealed record PowerBiProject(string PbipPath, string? SemanticModelDir, string? ReportDir)
{
    public string Name => Path.GetFileNameWithoutExtension(PbipPath);

    /// <summary>Procura um .pbip até 3 níveis abaixo da raiz do repositório (ignora .git, bin, obj).</summary>
    public static PowerBiProject? Detect(string repoPath)
    {
        try
        {
            var pbip = Find(repoPath, depth: 0);
            if (pbip == null) return null;
            var dir = Path.GetDirectoryName(pbip)!;
            var name = Path.GetFileNameWithoutExtension(pbip);
            string? Existing(string suffix) => Directory.Exists(Path.Combine(dir, name + suffix)) ? Path.Combine(dir, name + suffix) : null;
            return new PowerBiProject(pbip, Existing(".SemanticModel"), Existing(".Report"));
        }
        catch { return null; }
    }

    private static string? Find(string dir, int depth)
    {
        if (depth > 3) return null;
        var here = Directory.EnumerateFiles(dir, "*.pbip").FirstOrDefault();
        if (here != null) return here;
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var n = Path.GetFileName(sub);
            if (n.StartsWith('.') || n is "bin" or "obj" or "node_modules") continue;
            if (Find(sub, depth + 1) is { } found) return found;
        }
        return null;
    }

    /// <summary>Orientação acrescentada ao prompt do plano quando o repositório é um PBIP.</summary>
    public string Guidance(string repoRoot) => NxText.T(
        $"\n\nEste repositório contém um projeto Power BI em formato PBIP: {Path.GetRelativePath(repoRoot, PbipPath)}.\n" +
        "Regras para alterar Power BI como código:\n" +
        "- Modelo semântico (tabelas, colunas, medidas DAX, relacionamentos): edite os arquivos TMDL em *.SemanticModel/definition/ " +
        "(ex.: tables/<Tabela>.tmdl, relationships.tmdl). Mantenha a indentação por TAB do TMDL.\n" +
        "- Relatório (páginas e visuais): edite os JSON do PBIR em *.Report/definition/ (pages/<id>/page.json, pages/<id>/visuals/<id>/visual.json). " +
        "Todo JSON precisa continuar válido — o Power BI Desktop não abre o projeto com JSON quebrado.\n" +
        "- Não altere cache.abf, .pbi/, localSettings.json nem arquivos binários.\n" +
        "- DAX: use os nomes exatos de tabelas e colunas que existem no TMDL; explique cada medida nova no plano.",
        $"\n\nThis repository contains a Power BI project in PBIP format: {Path.GetRelativePath(repoRoot, PbipPath)}.\n" +
        "Rules for changing Power BI as code:\n" +
        "- Semantic model (tables, columns, DAX measures, relationships): edit the TMDL files in *.SemanticModel/definition/ " +
        "(e.g. tables/<Table>.tmdl, relationships.tmdl). Keep TMDL TAB indentation.\n" +
        "- Report (pages and visuals): edit the PBIR JSON in *.Report/definition/ (pages/<id>/page.json, pages/<id>/visuals/<id>/visual.json). " +
        "Every JSON must stay valid — Power BI Desktop will not open the project with broken JSON.\n" +
        "- Do not change cache.abf, .pbi/, localSettings.json or binary files.\n" +
        "- DAX: use the exact table and column names that exist in the TMDL; explain each new measure in the plan.");

    /// <summary>
    /// Validação automática depois da execução: todo JSON alterado do projeto (PBIR, .pbip,
    /// .pbism/.pbir) precisa continuar válido. JSON quebrado = achado CRÍTICO.
    /// </summary>
    public static List<Finding> ValidateChangedFiles(string worktree, string diff)
    {
        var findings = new List<Finding>();
        foreach (var line in diff.Split('\n'))
        {
            if (!line.StartsWith("+++ b/")) continue;
            var rel = line[6..].Trim();
            var ext = Path.GetExtension(rel).ToLowerInvariant();
            if (ext is not (".json" or ".pbip" or ".pbir" or ".pbism")) continue;
            var path = Path.Combine(worktree, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            try
            {
                using var _ = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException ex)
            {
                findings.Add(new Finding("Critico", rel, (int?)ex.LineNumber + 1,
                    NxText.T($"JSON inválido — o Power BI Desktop não vai abrir o projeto: {ex.Message}", $"Invalid JSON — Power BI Desktop will not open the project: {ex.Message}")));
            }
        }
        return findings;
    }
}
