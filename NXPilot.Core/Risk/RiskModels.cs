// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Risk;

/// <summary>Nível de risco de uma ação pedida por um agente (Claude Code, Codex...).</summary>
public enum RiskLevel { Baixo, Medio, Alto }

/// <summary>Resultado de uma classificação: nível, confiança (0–1), motor e tempo gasto.</summary>
public sealed record RiskResult(RiskLevel Level, double Confidence, string Engine, string? Reason, TimeSpan Elapsed);

/// <summary>
/// Classificador de risco — uma fachada por motor (regras, ONNX, LLM). O DecisionGate
/// consulta o classificador antes de aprovar sozinho ou escalar a decisão ao usuário.
/// </summary>
public interface IRiskClassifier : IDisposable
{
    string Name { get; }

    /// <summary>Carrega modelo/sessão. Chamado uma vez; o tempo de carga é medido à parte.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    Task<RiskResult> ClassifyAsync(string action, CancellationToken ct = default);
}

/// <summary>Exemplo rotulado — semente do classificador e, no futuro, cada decisão do usuário.</summary>
public sealed record RiskExample(string Action, RiskLevel Level);
