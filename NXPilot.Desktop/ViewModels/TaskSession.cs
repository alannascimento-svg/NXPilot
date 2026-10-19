// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using CommunityToolkit.Mvvm.ComponentModel;
using NXPilot.Agents;
using static NXPilot.NxText;

namespace NXPilot.Desktop.ViewModels;

public enum TaskState { Preparing, Planning, AwaitingApproval, Executing, Verifying, Done, Failed, Discarded, Stopped }

/// <summary>Quem revisa o resultado: Codex (somente leitura), o próprio Claude, ou ninguém.</summary>
public enum ReviewerKind { Codex, Claude, None }

/// <summary>Quem executa a tarefa (planeja e escreve) — sempre sob o DecisionGate.</summary>
public enum ExecutorKind { Claude, Codex }

/// <summary>O que a pessoa preencheu no assistente de Nova tarefa.</summary>
public sealed record NewTaskInfo(string RepoPath, string Title, string Intent, string Criteria, string? Model,
    ReviewerKind Reviewer = ReviewerKind.Codex, string? CodexModel = null, ExecutorKind Executor = ExecutorKind.Claude);

/// <summary>Registro salvo no Histórico (%LOCALAPPDATA%\NXPilot\tasks\&lt;id&gt;.json).</summary>
public sealed record TaskRecord(
    string Id, NewTaskInfo Info, TaskState State, DateTime CreatedAt, DateTime UpdatedAt,
    string? Worktree, string? Branch, string? BaseCommit, string? SessionId,
    string PlanText, List<PlannedPhase> Phases, List<string> PhaseStatus,
    VerificationResult? Verification, decimal CostUsd, List<LogEntry> Log);

/// <summary>
/// Uma tarefa real: intenção → plano em fases (agente em modo plan) → sua aprovação → fases
/// executadas no worktree (um commit por fase) → verificação cruzada → diff. Cada estado
/// libera só os botões que fazem sentido nele.
/// </summary>
public sealed partial class TaskSession(NewTaskInfo info, string id) : ObservableObject
{
    public string Id { get; } = id;
    public NewTaskInfo Info { get; } = info;
    public string Title => Info.Title;
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StateLabel), nameof(CanApprove), nameof(IsRunning), nameof(CanDiscard), nameof(IsFinished))]
    private TaskState _state = TaskState.Preparing;

    [ObservableProperty] private string? _worktree;
    [ObservableProperty] private string? _branch;
    [ObservableProperty] private string? _baseCommit;
    [ObservableProperty] private string? _sessionId;
    [ObservableProperty] private string _planText = "";

    /// <summary>Projeto Power BI (.pbip) encontrado no worktree; nulo se não for um projeto PBIP.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPowerBi))] private NXPilot.Integrations.PowerBiProject? _powerBi;
    public bool HasPowerBi => PowerBi != null;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CostLabel))] private decimal _costUsd;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StateLabel))] private VerificationResult? _verification;

    /// <summary>Fases do plano e o status de cada uma ("Pendente", "Rodando", "Concluido").</summary>
    public List<PlannedPhase> Phases { get; set; } = new();
    public List<string> PhaseStatus { get; set; } = new();

    /// <summary>Log desta tarefa — vai para o Histórico (trilha de auditoria por tarefa).</summary>
    public List<LogEntry> Log { get; } = new();

    public bool CanApprove => State == TaskState.AwaitingApproval;
    public bool CanDiscard => State == TaskState.AwaitingApproval;
    public bool IsRunning => State is TaskState.Preparing or TaskState.Planning or TaskState.Executing or TaskState.Verifying;
    public bool IsFinished => State is TaskState.Done or TaskState.Failed or TaskState.Discarded or TaskState.Stopped;

    public string CostLabel => CostUsd > 0 ? T($"custo informado pelos agentes: US$ {CostUsd:0.00}", $"cost reported by the agents: US$ {CostUsd:0.00}", $"costo informado por los agentes: US$ {CostUsd:0.00}") : "";

    public string StateLabel => State switch
    {
        TaskState.Preparing => T("Preparando o worktree…", "Preparing the worktree…", "Preparando el worktree…"),
        TaskState.Planning => T($"{(Info.Executor == ExecutorKind.Codex ? "Codex" : "Claude Code")} está planejando (somente leitura)…", $"{(Info.Executor == ExecutorKind.Codex ? "Codex" : "Claude Code")} is planning (read-only)…", $"{(Info.Executor == ExecutorKind.Codex ? "Codex" : "Claude Code")} está planificando (solo lectura)…"),
        TaskState.AwaitingApproval => T($"Plano pronto ({Phases.Count} fase(s)) — aguardando a sua aprovação", $"Plan ready ({Phases.Count} phase(s)) — waiting for your approval", $"Plan listo ({Phases.Count} fase(s)) — esperando su aprobación"),
        TaskState.Executing => T($"Executando a fase {PhaseStatus.IndexOf("Rodando") + 1} de {Phases.Count} — ações passam pelo gate", $"Running phase {PhaseStatus.IndexOf("Rodando") + 1} of {Phases.Count} — actions go through the gate", $"Ejecutando la fase {PhaseStatus.IndexOf("Rodando") + 1} de {Phases.Count} — las acciones pasan por el gate"),
        TaskState.Verifying => T("Revisando o diff em sessão nova (somente leitura)…", "Reviewing the diff in a new session (read-only)…", "Revisando el diff en una sesión nueva (solo lectura)…"),
        TaskState.Done when Verification is { Approved: false } => T("Execução concluída; revisão não aprovada — veja os detalhes", "Execution complete; review not approved — see details", "Ejecución completada; revisión no aprobada — vea los detalles"),
        TaskState.Done when Verification == null => T("Concluída sem revisão — veja o resultado na aba Diff", "Done without review — see the result in the Diff tab", "Concluida sin revisión — vea el resultado en la pestaña Diff"),
        TaskState.Done => T("Concluída e verificada — veja o resultado na aba Diff", "Done and verified — see the result in the Diff tab", "Concluida y verificada — vea el resultado en la pestaña Diff"),
        TaskState.Failed => T("Falhou — veja o Terminal e o Log", "Failed — see the Terminal and the Log", "Falló — vea el Terminal y el Log"),
        TaskState.Discarded => T("Plano descartado", "Plan discarded", "Plan descartado"),
        TaskState.Stopped => T("Interrompida por você", "Stopped by you", "Interrumpida por usted"),
        _ => State.ToString()
    };

    /// <summary>Atualiza os textos calculados (troca de idioma, mudança de fase).</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(CostLabel));
    }

    public TaskRecord ToRecord() => new(Id, Info, State, CreatedAt, DateTime.Now, Worktree, Branch, BaseCommit, SessionId,
        PlanText, Phases, PhaseStatus, Verification, CostUsd, Log.ToList());

    public static TaskSession FromRecord(TaskRecord r)
    {
        var s = new TaskSession(r.Info, r.Id)
        {
            CreatedAt = r.CreatedAt,
            Worktree = r.Worktree, Branch = r.Branch, BaseCommit = r.BaseCommit, SessionId = r.SessionId,
            PlanText = r.PlanText, CostUsd = r.CostUsd, Verification = r.Verification,
            Phases = r.Phases, PhaseStatus = r.PhaseStatus,
            // Tarefa que estava rodando quando o app fechou não continua sozinha.
            State = r.State is TaskState.Preparing or TaskState.Planning or TaskState.Executing or TaskState.Verifying ? TaskState.Stopped : r.State,
        };
        s.Log.AddRange(r.Log);
        return s;
    }
}
