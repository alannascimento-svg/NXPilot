// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Globalization;
using System.Text;

namespace NXPilot.Risk;

/// <summary>Prompt e leitura da resposta comuns a todo LLM da cascata (local ou remoto).</summary>
public static class RiskPrompt
{
    public const string System =
        "Você classifica o risco de ações que um agente de programação quer executar num repositório.\n" +
        "ALTO: apaga dados/arquivos, reescreve histórico remoto, mexe em produção, nuvem, segredos ou no sistema.\n" +
        "MEDIO: instala dependências, faz commit/push normal, cria ou edita arquivos do projeto.\n" +
        "BAIXO: apenas lê, lista, busca, compila ou roda testes.\n" +
        "Responda com UMA palavra: BAIXO, MEDIO ou ALTO.";

    public static RiskLevel Parse(string answer, out bool understood)
    {
        var sb = new StringBuilder();
        foreach (var c in answer.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var a = sb.ToString().ToUpperInvariant();
        understood = a.Contains("ALTO") || a.Contains("BAIXO") || a.Contains("MEDIO");
        return a.Contains("ALTO") ? RiskLevel.Alto : a.Contains("BAIXO") ? RiskLevel.Baixo : RiskLevel.Medio;
    }
}
