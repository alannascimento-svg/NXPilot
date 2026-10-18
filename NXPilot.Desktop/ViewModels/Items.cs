// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using CommunityToolkit.Mvvm.ComponentModel;
using NXPilot.Benchmark;
using NXPilot.Risk;

namespace NXPilot.Desktop.ViewModels;

public sealed record AgentItem(string Name, string Status, string Task, string Elapsed, IReadOnlyList<AgentItem>? Children = null);

public sealed record ArtifactItem(string Glyph, string Title, string Subtitle);

public sealed record FileChangeItem(string Path, string Kind, string Description);

public sealed record PhaseItem(int Number, string Title, string Status, string Agent, double Progress, IReadOnlyList<FileChangeItem> Files)
{
    /// <summary>Status é chave interna (cor do selo); este é o texto exibido.</summary>
    public string StatusLabel => Status switch
    {
        "Concluido" => NxText.T("Concluído", "Done"),
        "Rodando" => NxText.T("Rodando", "Running"),
        "Pendente" => NxText.T("Pendente", "Pending"),
        _ => Status
    };
}

/// <summary>Gravidade no estilo da verificação por fase: Critico, Maior, Menor.</summary>
public sealed record VerificationItem(string Severity, string File, string Message);

public sealed record DiffLine(string Text, string Kind);

public sealed record LogEntry(DateTime Time, string Source, string Kind, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");
}

/// <summary>Ação pedida por um agente, aguardando o DecisionGate (classificação + usuário).</summary>
public sealed partial class DecisionItem(string agent, string action, string context) : ObservableObject
{
    public string Agent { get; } = agent;
    public string Action { get; } = action;
    public string Context { get; } = context;
    public DateTime RequestedAt { get; } = DateTime.Now;
    public string TimeText => RequestedAt.ToString("HH:mm:ss");

    /// <summary>
    /// Pedido vindo de um agente REAL (hook PreToolUse): o agente fica parado até Aprovar/Negar
    /// completar esta resposta. Nulo nas ações simuladas.
    /// </summary>
    public TaskCompletionSource<bool>? Response { get; init; }

    [ObservableProperty] private RiskLevel? _level;
    [ObservableProperty] private string _riskDetail = NxText.T("aguardando classificador…", "waiting for the classifier…");
}

public sealed record BenchmarkRow(string Engine, string Load, double AverageMs, string Average, string P95, string Accuracy,
    int MissedHigh, double BarWidth, string? Error, IReadOnlyList<BenchmarkCase> Cases)
{
    public static BenchmarkRow From(BenchmarkReport r, double maxBar = 220)
    {
        // Barra em escala log: 0,1 ms → quase nada; 10 s → cheia. Linear esconderia o ONNX.
        var w = r.Error != null ? 0 : Math.Clamp((Math.Log10(Math.Max(r.AverageMs, 0.1)) + 1) / 5 * maxBar, 4, maxBar);
        return new BenchmarkRow(AiLabViewModel.ShortEngine(r.Engine), $"{r.Load.TotalMilliseconds:N0} ms", r.AverageMs, FormatMs(r.AverageMs),
            FormatMs(r.P95Ms), r.Error == null ? $"{r.CorrectCount}/{r.Cases.Count}" : "—", r.MissedHigh, w, r.Error, r.Cases);
    }

    public static string FormatMs(double ms) => ms >= 1000 ? $"{ms / 1000:0.00} s" : $"{ms:0.0} ms";
}

/// <summary>Resultado do "testar ação" — um por motor carregado.</summary>
public sealed record TryRow(string Engine, RiskLevel Level, string Confidence, string Time, string? Reason);
