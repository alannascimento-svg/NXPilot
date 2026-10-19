// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text.Json;
using System.Text.RegularExpressions;

namespace NXPilot.Agents;

public sealed record PlannedFile(string Path, string Change, string Why);

public sealed record PlannedPhase(int Number, string Title, IReadOnlyList<PlannedFile> Files);

/// <summary>Achado da verificação. Severity: "Critico", "Maior" ou "Menor" (mesmas chaves das cores da tela).</summary>
public sealed record Finding(string Severity, string File, int? Line, string Message);

public sealed record VerificationResult(bool Approved, string Summary, IReadOnlyList<Finding> Findings, string Reviewer);

/// <summary>
/// Prompts e leitura das respostas estruturadas do plano (fases) e da verificação (achados).
/// O texto livre do agente é para a pessoa; o JSON é para o NXPilot — e a leitura é
/// tolerante: sem JSON válido, o plano vira uma fase única e a verificação vira um aviso.
/// </summary>
public static class TaskPlanning
{
    // ------------------------------------------------------------------ plano

    public static string PlanPrompt(string title, string intent, string criteria) => NxText.T(
        $"Tarefa: {title}\n\nIntenção:\n{intent}\n\nCritérios de aceite:\n{criteria}\n\n" +
        "Analise o repositório e proponha um plano de implementação objetivo, dividido em fases pequenas " +
        "(1 a 4 fases; cada fase deve deixar o projeto funcionando). Não altere nada agora.\n\n" +
        "Escreva o plano para uma pessoa ler e, NO FINAL, inclua um bloco ```json exatamente neste formato:\n" +
        "{\"phases\":[{\"title\":\"...\",\"files\":[{\"path\":\"caminho/relativo\",\"change\":\"new|modify|delete\",\"why\":\"...\"}]}]}",
        $"Task: {title}\n\nIntent:\n{intent}\n\nAcceptance criteria:\n{criteria}\n\n" +
        "Analyze the repository and propose a concise implementation plan, split into small phases " +
        "(1 to 4 phases; each phase must leave the project working). Do not change anything now.\n\n" +
        "Write the plan for a person to read and, AT THE END, include a ```json block exactly in this format:\n" +
        "{\"phases\":[{\"title\":\"...\",\"files\":[{\"path\":\"relative/path\",\"change\":\"new|modify|delete\",\"why\":\"...\"}]}]}",
        $"Tarea: {title}\n\nIntención:\n{intent}\n\nCriterios de aceptación:\n{criteria}\n\n" +
        "Analice el repositorio y proponga un plan de implementación objetivo, dividido en fases pequeñas " +
        "(de 1 a 4 fases; cada fase debe dejar el proyecto funcionando). No modifique nada ahora.\n\n" +
        "Escriba el plan para que una persona lo lea y, AL FINAL, incluya un bloque ```json exactamente en este formato:\n" +
        "{\"phases\":[{\"title\":\"...\",\"files\":[{\"path\":\"ruta/relativa\",\"change\":\"new|modify|delete\",\"why\":\"...\"}]}]}");

    public static string PhasePrompt(PlannedPhase phase, int total) => NxText.T(
        $"Plano aprovado. Execute agora SOMENTE a Fase {phase.Number} de {total}: {phase.Title}. " +
        "Trabalhe só dentro deste diretório e não avance para a próxima fase. Ao terminar, resuma o que mudou.",
        $"Plan approved. Execute ONLY Phase {phase.Number} of {total} now: {phase.Title}. " +
        "Work only inside this directory and do not move on to the next phase. When done, summarize what changed.",
        $"Plan aprobado. Ejecute ahora SOLO la Fase {phase.Number} de {total}: {phase.Title}. " +
        "Trabaje solo dentro de este directorio y no avance a la siguiente fase. Al terminar, resuma lo que cambió.");

    /// <summary>Fases do bloco ```json no fim do plano; sem bloco válido, uma fase única.</summary>
    public static IReadOnlyList<PlannedPhase> ParsePhases(string planText)
    {
        try
        {
            if (LastJsonBlock(planText) is { } json)
            {
                using var doc = JsonDocument.Parse(json);
                var phases = new List<PlannedPhase>();
                foreach (var p in doc.RootElement.GetProperty("phases").EnumerateArray())
                {
                    var files = new List<PlannedFile>();
                    if (p.TryGetProperty("files", out var fs) && fs.ValueKind == JsonValueKind.Array)
                        foreach (var f in fs.EnumerateArray())
                            files.Add(new PlannedFile(Str(f, "path") ?? "?", Str(f, "change") ?? "modify", Str(f, "why") ?? ""));
                    phases.Add(new PlannedPhase(phases.Count + 1, Str(p, "title") ?? NxText.T($"Fase {phases.Count + 1}", $"Phase {phases.Count + 1}", $"Fase {phases.Count + 1}"), files));
                }
                if (phases.Count > 0) return phases;
            }
        }
        catch (Exception) { /* JSON fora do formato: cai na fase única */ }
        return new[] { new PlannedPhase(1, NxText.T("Executar o plano", "Execute the plan", "Ejecutar el plan"), Array.Empty<PlannedFile>()) };
    }

    /// <summary>O plano para a pessoa, sem o bloco JSON técnico do final.</summary>
    public static string HumanPlan(string planText)
    {
        var i = planText.LastIndexOf("```json", StringComparison.OrdinalIgnoreCase);
        return (i >= 0 ? planText[..i] : planText).TrimEnd();
    }

    // ------------------------------------------------------------------ verificação

    /// <summary>Esquema para o --output-schema do Codex (todas as chaves obrigatórias, sem extras).</summary>
    public const string VerificationSchema = """
    {"type":"object","additionalProperties":false,"required":["approved","summary","findings"],
     "properties":{
       "approved":{"type":"boolean"},
       "summary":{"type":"string"},
       "findings":{"type":"array","items":{"type":"object","additionalProperties":false,
         "required":["severity","file","line","message"],
         "properties":{
           "severity":{"type":"string","enum":["critical","major","minor"]},
           "file":{"type":"string"},
           "line":{"type":["integer","null"]},
           "message":{"type":"string"}}}}}}
    """;

    public static string VerificationPrompt(string title, string criteria, string plan, string diff) => NxText.T(
        "Você é o REVISOR de uma mudança feita por outro agente de código. Não execute comandos: tudo o que precisa está abaixo.\n" +
        "Compare o diff com o plano aprovado e com os critérios de aceite. Aponte erros, riscos, coisas fora do plano e critérios não atendidos.\n" +
        "Gravidade: critical = quebra, perda de dados, segurança ou critério de aceite não atendido; major = defeito real ou desvio relevante do plano; minor = melhoria.\n" +
        "approved = true só se não houver achado critical. Escreva summary e message em português.\n" +
        "Responda SOMENTE com JSON: {\"approved\":bool,\"summary\":\"...\",\"findings\":[{\"severity\":\"critical|major|minor\",\"file\":\"...\",\"line\":número ou null,\"message\":\"...\"}]}\n\n" +
        $"TAREFA: {title}\n\nCRITÉRIOS DE ACEITE:\n{criteria}\n\nPLANO APROVADO:\n{Trim(plan, 6000)}\n\nDIFF:\n{Trim(diff, 30000)}",
        "You are the REVIEWER of a change made by another coding agent. Do not run commands: everything you need is below.\n" +
        "Compare the diff with the approved plan and the acceptance criteria. Point out bugs, risks, off-plan changes and unmet criteria.\n" +
        "Severity: critical = breakage, data loss, security or unmet acceptance criterion; major = real defect or relevant deviation from the plan; minor = improvement.\n" +
        "approved = true only if there is no critical finding. Write summary and message in English.\n" +
        "Reply ONLY with JSON: {\"approved\":bool,\"summary\":\"...\",\"findings\":[{\"severity\":\"critical|major|minor\",\"file\":\"...\",\"line\":number or null,\"message\":\"...\"}]}\n\n" +
        $"TASK: {title}\n\nACCEPTANCE CRITERIA:\n{criteria}\n\nAPPROVED PLAN:\n{Trim(plan, 6000)}\n\nDIFF:\n{Trim(diff, 30000)}",
        "Usted es el REVISOR de un cambio hecho por otro agente de código. No ejecute comandos: todo lo que necesita está abajo.\n" +
        "Compare el diff con el plan aprobado y con los criterios de aceptación. Señale errores, riesgos, cambios fuera del plan y criterios no cumplidos.\n" +
        "Gravedad: critical = rotura, pérdida de datos, seguridad o criterio de aceptación no cumplido; major = defecto real o desviación relevante del plan; minor = mejora.\n" +
        "approved = true solo si no hay ningún hallazgo critical. Escriba summary y message en español.\n" +
        "Responda SOLO con JSON: {\"approved\":bool,\"summary\":\"...\",\"findings\":[{\"severity\":\"critical|major|minor\",\"file\":\"...\",\"line\":número o null,\"message\":\"...\"}]}\n\n" +
        $"TAREA: {title}\n\nCRITERIOS DE ACEPTACIÓN:\n{criteria}\n\nPLAN APROBADO:\n{Trim(plan, 6000)}\n\nDIFF:\n{Trim(diff, 30000)}");

    public static VerificationResult ParseVerification(string text, string reviewer)
    {
        try
        {
            var json = LastJsonBlock(text) ?? FirstJsonObject(text);
            if (json != null)
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var findings = new List<Finding>();
                if (root.TryGetProperty("findings", out var fs) && fs.ValueKind == JsonValueKind.Array)
                    foreach (var f in fs.EnumerateArray())
                        findings.Add(new Finding(
                            (Str(f, "severity") ?? "minor") switch { "critical" => "Critico", "major" => "Maior", _ => "Menor" },
                            Str(f, "file") ?? "",
                            f.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : null,
                            Str(f, "message") ?? ""));
                var approved = root.TryGetProperty("approved", out var a) && a.ValueKind == JsonValueKind.True
                               && findings.All(x => x.Severity != "Critico");
                return new VerificationResult(approved, Str(root, "summary") ?? "", findings, reviewer);
            }
        }
        catch (Exception) { /* resposta fora do formato */ }
        return new VerificationResult(false, NxText.T("O revisor não devolveu o JSON esperado; leia a resposta no Terminal.", "The reviewer did not return the expected JSON; read the answer in the Terminal.", "El revisor no devolvió el JSON esperado; lea la respuesta en la Terminal."),
            new[] { new Finding("Maior", "", null, Trim(text, 400)) }, reviewer);
    }

    private static string? LastJsonBlock(string text)
    {
        var matches = Regex.Matches(text, "```json\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return matches.Count > 0 ? matches[^1].Groups[1].Value : null;
    }

    private static string? FirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static string Trim(string s, int max) => s.Length > max ? s[..max] + "\n…" : s;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
