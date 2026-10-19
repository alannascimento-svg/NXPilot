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
    public const string Spanish = "es-ES";

    public static string Language { get; set; } = Portuguese;

    public static bool IsEnglish => Language == English;

    public static bool IsSpanish => Language == Spanish;

    /// <summary>Português, inglês ou espanhol conforme o idioma ativo (sem texto em espanhol, vale o inglês).</summary>
    public static string T(string pt, string en, string? es = null) => IsSpanish ? es ?? en : IsEnglish ? en : pt;

    /// <summary>Código de idioma suportado: "es…" → espanhol, "en…" → inglês, o resto → português.</summary>
    public static string Normalize(string code) =>
        code.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? Spanish
        : code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? English
        : Portuguese;

    /// <summary>Idioma do Windows: pt → português, es → espanhol, senão inglês.</summary>
    public static string FromSystem(string twoLetter) => twoLetter switch { "pt" => Portuguese, "es" => Spanish, _ => English };
}
