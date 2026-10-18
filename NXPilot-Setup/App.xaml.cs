// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace NXPilot.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Mesmo idioma do app, se já foi escolhido nele; senão, o do Windows (como no NXProject-Setup).
        ApplyLanguage(SavedLanguage() ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? NxText.Portuguese : NxText.English));
        base.OnStartup(e);
    }

    /// <summary>Troca o dicionário de textos ao vivo (DynamicResource) e o idioma do NxText.</summary>
    public static void ApplyLanguage(string code)
    {
        code = code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? NxText.English : NxText.Portuguese;
        NxText.Language = code;
        var resources = Current.Resources.MergedDictionaries;
        for (var i = resources.Count - 1; i >= 0; i--)
            if (resources[i].Source?.OriginalString.Contains("Strings/Strings.") == true) resources.RemoveAt(i);
        resources.Add(new ResourceDictionary { Source = new Uri($"Strings/Strings.{code}.xaml", UriKind.Relative) });
    }

    /// <summary>Idioma salvo pelo app (%LOCALAPPDATA%\NXPilot\settings.json); nulo se não houver.</summary>
    private static string? SavedLanguage()
    {
        try
        {
            // NXAgentic = nome antigo do produto público (o app migra os dados no 1º início).
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var file = new[] { "NXPilot", "NXAgentic" }.Select(d => Path.Combine(local, d, "settings.json")).FirstOrDefault(File.Exists);
            if (file == null) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty("Language", out var l) ? l.GetString() : null;
        }
        catch { return null; }
    }
}
