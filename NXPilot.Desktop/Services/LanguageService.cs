// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;

namespace NXPilot.Desktop.Services;

/// <summary>
/// Troca de idioma AO VIVO, no mesmo padrão do NXProject: substitui o Strings.{código}.xaml
/// nos recursos do app (os textos em DynamicResource mudam na hora), avisa o
/// <see cref="NxText"/> da biblioteca e dispara <see cref="LanguageChanged"/> para os
/// ViewModels refazerem os textos que eles geram. A escolha fica salva em settings.json.
/// </summary>
public static class LanguageService
{
    public static string CurrentLanguage => NxText.Language;

    public static event Action? LanguageChanged;

    /// <summary>Idioma salvo; sem escolha salva, segue o Windows (pt → português, es → espanhol, senão inglês).</summary>
    public static string LoadPreferred()
    {
        if (AppSettings.Load().Language is { } saved) return Normalize(saved);
        return NxText.FromSystem(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    public static void Apply(string languageCode, bool save = false)
    {
        var code = Normalize(languageCode);
        NxText.Language = code;

        var culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var app = Application.Current;
        if (app == null) return;
        if (app.MainWindow != null)
            app.MainWindow.Language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);

        for (var i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            if (app.Resources.MergedDictionaries[i].Source?.OriginalString.Contains("Strings/Strings.") == true)
                app.Resources.MergedDictionaries.RemoveAt(i);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"Strings/Strings.{code}.xaml", UriKind.Relative) });

        if (save) Save(code);
        LanguageChanged?.Invoke();
    }

    /// <summary>Texto de um Strings.*.xaml pela chave, com format opcional (para o code-behind).</summary>
    public static string Str(string key, params object[] args)
    {
        var val = Application.Current?.TryFindResource(key) as string ?? key;
        return args.Length > 0 ? string.Format(val, args) : val;
    }

    private static string Normalize(string code) => NxText.Normalize(code);

    private static void Save(string code) => AppSettings.Update(s => s.Language = code);
}
