// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using NXPilot.Agents;

namespace NXPilot.Planning;

/// <summary>O que o planejador precisa saber da tarefa. Extra = orientação de contexto (ex.: projeto Power BI).</summary>
public sealed record PlanRequest(string Title, string Intent, string Criteria, string? Extra = null);

/// <summary>Plano lido da resposta do agente: fases (para o NXPilot) e texto (para a pessoa).</summary>
public sealed record PlanOutcome(IReadOnlyList<PlannedPhase> Phases, string HumanPlan);

/// <summary>
/// FACHADA de planejamento (contrato público do Open Core). O executor só conhece esta interface:
/// o NXPilot Community usa o <see cref="BasicAgentPlanner"/>; o NXAgentic (comercial) pluga um
/// planejador avançado pelo mesmo contrato — hoje um processo local, no futuro um serviço remoto
/// — sem mudar o executor. O planejador só ESCREVE prompts e LÊ planos: quem age é o agente,
/// sempre sob o gate. Nenhum planejador consegue liberar ação sem passar pela cascata e por você.
/// </summary>
public interface IAgentPlanner
{
    string Name { get; }
    Task<string> PlanPromptAsync(PlanRequest request, CancellationToken ct = default);
    Task<PlanOutcome> ReadPlanAsync(string agentAnswer, CancellationToken ct = default);
    Task<string> PhasePromptAsync(PlannedPhase phase, int totalPhases, CancellationToken ct = default);
}

/// <summary>Planejador do NXPilot Community: o plano em fases de sempre (TaskPlanning). Também é o fallback de qualquer outro.</summary>
public sealed class BasicAgentPlanner : IAgentPlanner
{
    public string Name => "Basic";

    public Task<string> PlanPromptAsync(PlanRequest r, CancellationToken ct = default) =>
        Task.FromResult(TaskPlanning.PlanPrompt(r.Title, r.Intent, r.Criteria) + (r.Extra ?? ""));

    public Task<PlanOutcome> ReadPlanAsync(string agentAnswer, CancellationToken ct = default) =>
        Task.FromResult(new PlanOutcome(TaskPlanning.ParsePhases(agentAnswer), TaskPlanning.HumanPlan(agentAnswer)));

    public Task<string> PhasePromptAsync(PlannedPhase phase, int totalPhases, CancellationToken ct = default) =>
        Task.FromResult(TaskPlanning.PhasePrompt(phase, totalPhases));
}
