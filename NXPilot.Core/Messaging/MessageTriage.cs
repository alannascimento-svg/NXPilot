// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Messaging;

/// <summary>
/// Primeiro degrau da cascata aplicada a mensagens: regras instantâneas por remetente e
/// palavra-chave. Os próximos degraus (ONNX por assunto; LLM com visão para o relatório
/// técnico) entram depois, só para o que passar daqui — mesmo pilar do DecisionGate.
/// </summary>
public sealed class RuleMessageTriage(IEnumerable<string> prioritySenders, IEnumerable<string> keywords)
{
    private readonly HashSet<string> _priority = prioritySenders.Select(Digits).Where(s => s.Length > 0).ToHashSet();
    private readonly string[] _keywords = keywords.Select(k => k.Trim()).Where(k => k.Length > 0).ToArray();

    private static string Engine => NxText.T("Regras", "Rules", "Reglas");

    public TriageResult Classify(IncomingMessage m)
    {
        if (_priority.Contains(Digits(m.FromId)))
            return new TriageResult(MessageRelevance.Prioritario, NxText.T("remetente prioritário", "priority sender", "remitente prioritario"), Engine);

        var hit = _keywords.FirstOrDefault(k => m.Text?.Contains(k, StringComparison.OrdinalIgnoreCase) == true);
        if (hit != null)
            return new TriageResult(MessageRelevance.Relevante, NxText.T("assunto: ", "subject: ", "asunto: ") + $"\"{hit}\"", Engine);

        // Imagem sem texto pode ser foto de equipamento/laudo: não descarta, deixa para o próximo degrau.
        if (m.Media.Count > 0)
            return new TriageResult(MessageRelevance.Relevante, NxText.T("contém mídia — revisar", "contains media — review", "contiene multimedia — revisar"), Engine);

        return new TriageResult(MessageRelevance.Ignorar, NxText.T("fora dos remetentes e assuntos monitorados", "outside the monitored senders and subjects", "fuera de los remitentes y asuntos monitoreados"), Engine);
    }

    /// <summary>"+55 (11) 9 8888-7777" e "5511988887777" são o mesmo remetente.</summary>
    private static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());
}
