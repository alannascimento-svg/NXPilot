// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot;

/// <summary>
/// Idioma dos textos gerados em C# (status, log, motivos da classificação, lições). Mesmo
/// papel do NxText do NXProject: a biblioteca não enxerga os Strings.*.xaml do WPF, então o
/// app avisa aqui qual idioma está ativo e cada texto traz as duas versões lado a lado.
/// </summary>
public static class NxText
{
    public const string Portuguese = "pt-BR";
    public const string English = "en-US";

    public static string Language { get; set; } = Portuguese;

    public static bool IsEnglish => Language == English;

    /// <summary>Português ou inglês conforme o idioma ativo.</summary>
    public static string T(string pt, string en) => IsEnglish ? en : pt;
}
