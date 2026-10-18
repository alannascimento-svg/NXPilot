// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using System.Text.Json;

namespace NXPilot.Desktop.Services;

/// <summary>
/// Preferências do app num arquivo só (%LOCALAPPDATA%\NXPilot\settings.json): idioma e as
/// últimas escolhas da Nova tarefa. Ler e gravar sempre o objeto inteiro evita que salvar
/// uma preferência apague outra.
/// </summary>
public sealed class AppSettings
{
    public string? Language { get; set; }
    public string? LastRepo { get; set; }
    public string? LastModel { get; set; }
    public string? CodexModel { get; set; }
    public string? Reviewer { get; set; }
    public string? Executor { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* arquivo inválido: volta ao padrão */ }
        return new();
    }

    /// <summary>Carrega, aplica a mudança e grava — sem perder os outros campos.</summary>
    public static void Update(Action<AppSettings> change)
    {
        try
        {
            var s = Load();
            change(s);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* sem permissão: vale só nesta sessão */ }
    }
}
