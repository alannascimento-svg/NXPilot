// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using System.Text.Json;

namespace NXPilot.Desktop.Tutorial;

/// <summary>
/// Lições concluídas e se o guia abre sozinho, em %LOCALAPPDATA%\NXPilot\tutorial.json.
/// Falha de leitura/gravação nunca impede o app de abrir — no pior caso o guia reaparece.
/// </summary>
public sealed class TutorialProgress
{
    public HashSet<string> Completed { get; set; } = new();
    public bool ShowOnStartup { get; set; } = true;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "tutorial.json");

    public static TutorialProgress Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<TutorialProgress>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* arquivo corrompido: recomeça */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* sem permissão: o progresso só não persiste */ }
    }

    /// <summary>Primeira lição ainda não concluída, na ordem do catálogo.</summary>
    public Lesson? NextLesson() => LessonCatalog.All.FirstOrDefault(l => !Completed.Contains(l.Id));
}
