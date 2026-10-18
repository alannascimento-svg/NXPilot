// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXPilot.Agents;

/// <summary>Pedido do hook ao NXPilot: qual ferramenta o agente quer usar e com o quê.</summary>
public sealed record GateRequest(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("cwd")] string? Cwd,
    [property: JsonPropertyName("session")] string? SessionId);

public sealed record GateResponse(
    [property: JsonPropertyName("allow")] bool Allow,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>
/// Ponte entre o hook PreToolUse do Claude Code e o DecisionGate do app, por named pipe local
/// (não sai da máquina, não abre porta de rede). Um pedido por conexão: uma linha JSON de ida,
/// uma de volta. O nome do pipe vai para o agente na variável <see cref="PipeEnvVar"/>.
/// </summary>
public static class GatePipe
{
    public const string PipeEnvVar = "NXPILOT_GATE_PIPE";

    // ------------------------------------------------------------------ lado do app

    /// <summary>Escuta pedidos até o cancelamento; cada conexão é atendida em paralelo.</summary>
    public static async Task ServeAsync(string pipeName, Func<GateRequest, Task<GateResponse>> decide, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try { await server.WaitForConnectionAsync(ct); }
            catch (OperationCanceledException) { await server.DisposeAsync(); break; }
            _ = HandleAsync(server, decide, ct);
        }
    }

    private static async Task HandleAsync(NamedPipeServerStream server, Func<GateRequest, Task<GateResponse>> decide, CancellationToken ct)
    {
        await using (server)
        {
            GateResponse response;
            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var line = await reader.ReadLineAsync(ct);
                var request = line == null ? null : JsonSerializer.Deserialize<GateRequest>(line);
                response = request == null
                    ? new GateResponse(false, NxText.T("pedido vazio", "empty request"))
                    : await decide(request);
            }
            catch (Exception ex) { response = new GateResponse(false, "NXPilot: " + ex.Message); }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + "\n");
                await server.WriteAsync(bytes, ct);
                await server.FlushAsync(ct);
            }
            catch { /* hook já desistiu (timeout): nada a responder */ }
        }
    }

    // ------------------------------------------------------------------ lado do hook

    /// <summary>
    /// Corpo do hook (`NxPilot.exe --hook`): lê o JSON do PreToolUse no stdin, pergunta ao app
    /// e escreve a decisão no stdout. Qualquer falha vira NEGAR — nunca liberar por omissão.
    /// </summary>
    public static async Task<int> RunHookAsync(TextReader stdin, TextWriter stdout, string? pipeName)
    {
        GateResponse decision;
        try
        {
            using var doc = JsonDocument.Parse(await stdin.ReadToEndAsync());
            var root = doc.RootElement;
            var tool = root.TryGetProperty("tool_name", out var t) ? t.GetString() ?? "?" : "?";
            var input = root.TryGetProperty("tool_input", out var i) ? i : default;
            var request = new GateRequest(tool, ClaudeStreamParser.DescribeTool(tool, input),
                root.TryGetProperty("cwd", out var c) ? c.GetString() : null,
                root.TryGetProperty("session_id", out var s) ? s.GetString() : null);

            if (string.IsNullOrEmpty(pipeName))
                decision = new GateResponse(false, NxText.T("NXPilot não está escutando (hook fora do app).", "NXPilot is not listening (hook outside the app)."));
            else
            {
                await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(5000);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + "\n");
                await client.WriteAsync(bytes);
                await client.FlushAsync();
                using var reader = new StreamReader(client, Encoding.UTF8);
                var line = await reader.ReadLineAsync();
                decision = line == null
                    ? new GateResponse(false, NxText.T("sem resposta do NXPilot", "no answer from NXPilot"))
                    : JsonSerializer.Deserialize<GateResponse>(line) ?? new GateResponse(false, "?");
            }
        }
        catch (Exception ex) { decision = new GateResponse(false, "NXPilot: " + ex.Message); }

        // Formato documentado do PreToolUse: allow libera a ferramenta, deny bloqueia e o
        // motivo volta para o agente (ele pode ajustar o plano).
        var output = new
        {
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = decision.Allow ? "allow" : "deny",
                permissionDecisionReason = decision.Reason,
            }
        };
        await stdout.WriteLineAsync(JsonSerializer.Serialize(output));
        await stdout.FlushAsync();
        return 0;
    }
}
