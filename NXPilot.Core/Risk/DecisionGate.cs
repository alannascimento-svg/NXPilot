// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Risk;

public enum GateOutcome { AutoApprove, AskUser }

/// <summary>
/// Política do DecisionGate: só aprova sozinho o que é BAIXO com confiança suficiente.
/// Médio e Alto sempre vão para o usuário — o classificador acelera, nunca decide sozinho
/// algo perigoso. A política em si fica numa linha para ser auditável.
/// </summary>
public static class DecisionGate
{
    public const double AutoApproveConfidence = 0.6;

    public static GateOutcome Decide(RiskResult r) =>
        r.Level == RiskLevel.Baixo && r.Confidence >= AutoApproveConfidence ? GateOutcome.AutoApprove : GateOutcome.AskUser;
}
