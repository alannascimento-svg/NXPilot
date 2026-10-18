// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NXPilot.Agents;

namespace NXPilot.Planning;

/// <summary>
/// Como a mensagem chega ao planejador do NXAgentic. Hoje: um processo local (Native AOT,
/// assinado). Amanhã: HTTPS para o serviço da Nexus XData — mesma mensagem, outro transporte.
/// </summary>
public interface IIntelligenceTransport
{
    string Description { get; }
    Task<string> SendAsync(string requestJson, CancellationToken ct);
}

/// <summary>
/// Cliente do planejador avançado (NXAgentic Intelligence). Protocolo JSON v1, uma linha por
/// pedido: {"v":1,"op":"plan-prompt"|"read-plan"|"phase-prompt","lang":"pt-BR",...}. Qualquer
/// falha (sem resposta, demora, JSON fora do formato) cai no <see cref="BasicAgentPlanner"/>:
/// o módulo comercial pode melhorar o plano, mas nunca impedir o NXPilot de trabalhar.
/// </summary>
public sealed class IntelligencePlanner(IIntelligenceTransport transport, Action<string>? log = null) : IAgentPlanner
{
    public const int ProtocolVersion = 1;
    private readonly BasicAgentPlanner _fallback = new();

    public string Name => "NXAgentic Intelligence";

    public Task<string> PlanPromptAsync(PlanRequest r, CancellationToken ct = default) =>
        CallAsync("plan-prompt", new JsonObject { ["title"] = r.Title, ["intent"] = r.Intent, ["criteria"] = r.Criteria, ["extra"] = r.Extra },
            res => Text(res, "prompt"), () => _fallback.PlanPromptAsync(r, ct), ct);

    public Task<PlanOutcome> ReadPlanAsync(string agentAnswer, CancellationToken ct = default) =>
        CallAsync("read-plan", new JsonObject { ["answer"] = agentAnswer },
            res => ReadOutcome(res), () => _fallback.ReadPlanAsync(agentAnswer, ct), ct);

    public Task<string> PhasePromptAsync(PlannedPhase phase, int totalPhases, CancellationToken ct = default) =>
        CallAsync("phase-prompt", new JsonObject { ["phase"] = ToJson(phase), ["total"] = totalPhases },
            res => Text(res, "prompt"), () => _fallback.PhasePromptAsync(phase, totalPhases, ct), ct);

    private async Task<T> CallAsync<T>(string op, JsonObject body, Func<JsonElement, T> read, Func<Task<T>> fallback, CancellationToken ct)
    {
        body["v"] = ProtocolVersion;
        body["op"] = op;
        body["lang"] = NxText.Language;
        try
        {
            var answer = await transport.SendAsync(body.ToJsonString(), ct);
            using var doc = JsonDocument.Parse(answer);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True) return read(root);
            throw new InvalidOperationException(root.TryGetProperty("error", out var e) ? e.ToString() : "ok=false");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            log?.Invoke(NxText.T($"NXAgentic Intelligence indisponível em \"{op}\" ({ex.Message}) — usando o planejador Basic.",
                                 $"NXAgentic Intelligence unavailable for \"{op}\" ({ex.Message}) — using the Basic planner."));
            return await fallback();
        }
    }

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s
            ? s : throw new InvalidDataException($"\"{name}\" ausente");

    private static PlanOutcome ReadOutcome(JsonElement e)
    {
        var phases = new List<PlannedPhase>();
        foreach (var p in e.GetProperty("phases").EnumerateArray())
        {
            var files = p.TryGetProperty("files", out var fs) && fs.ValueKind == JsonValueKind.Array
                ? fs.EnumerateArray().Select(f => new PlannedFile(Str(f, "path") ?? "?", Str(f, "change") ?? "modify", Str(f, "why") ?? "")).ToList()
                : new List<PlannedFile>();
            phases.Add(new PlannedPhase(phases.Count + 1, Str(p, "title") ?? $"{phases.Count + 1}", files));
        }
        if (phases.Count == 0) throw new InvalidDataException("sem fases");
        return new PlanOutcome(phases, Str(e, "humanPlan") ?? "");
    }

    private static JsonObject ToJson(PlannedPhase p) => new()
    {
        ["number"] = p.Number,
        ["title"] = p.Title,
        ["files"] = new JsonArray(p.Files.Select(f => (JsonNode)new JsonObject { ["path"] = f.Path, ["change"] = f.Change, ["why"] = f.Why }).ToArray()),
    };

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>
/// Transporte local: roda o NXAgentic.Intelligence.exe, manda o pedido pelo stdin e lê UMA linha
/// JSON do stdout. Sem rede; o contexto do cliente não sai da máquina. Tempo-limite curto: se o
/// módulo travar, o processo é encerrado e o NXPilot segue com o Basic.
/// </summary>
public sealed class ProcessIntelligenceTransport(string exePath, TimeSpan? timeout = null) : IIntelligenceTransport
{
    public string Description => exePath;

    public async Task<string> SendAsync(string requestJson, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("não iniciou");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));
        using var kill = cts.Token.Register(() => { try { process.Kill(entireProcessTree: true); } catch { } });
        _ = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await process.StandardInput.WriteLineAsync(requestJson.AsMemory(), cts.Token);
        process.StandardInput.Close();
        var line = await process.StandardOutput.ReadLineAsync(cts.Token);
        return line ?? throw new InvalidDataException("sem resposta");
    }
}
