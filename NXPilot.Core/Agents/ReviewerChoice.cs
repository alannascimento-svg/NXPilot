// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Agents;

public enum AgentKind { Claude, Codex }

/// <summary>
/// Quem faz a verificação cruzada. O ideal é o OUTRO agente (outro modelo pega o que o primeiro
/// deixou passar); quem só tem um agente instalado não fica sem revisão: o mesmo agente revisa
/// numa sessão NOVA e somente leitura (não a que escreveu o código).
/// </summary>
public static class ReviewerChoice
{
    /// <returns>Agente revisor e se é o mesmo do executor; nulo se nenhum agente estiver instalado.</returns>
    public static (AgentKind Reviewer, bool SameAsExecutor)? Choose(AgentKind preferred, AgentKind executor, Func<AgentKind, bool> isInstalled)
    {
        var other = executor == AgentKind.Claude ? AgentKind.Codex : AgentKind.Claude;
        // Preferência = o próprio executor, mas o outro existe: cruza (é o que dá valor à revisão).
        if (preferred == executor && isInstalled(other)) return (other, false);
        if (isInstalled(preferred)) return (preferred, preferred == executor);
        if (isInstalled(other)) return (other, false);
        if (isInstalled(executor)) return (executor, true); // só o executor existe: ele revisa em sessão nova
        return null;
    }

    public static bool IsInstalled(AgentKind kind) =>
        (kind == AgentKind.Claude ? ClaudeCodeAdapter.Locate() : CodexAdapter.Locate()) != null;
}
