// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NXPilot.Desktop.ViewModels;

namespace NXPilot.Desktop.Services;

/// <summary>
/// Histórico das tarefas: um JSON por tarefa em %LOCALAPPDATA%\NXPilot\tasks, com plano,
/// fases, verificação, custo e o log de auditoria da tarefa. Arquivo simples de propósito
/// (sem banco) — dá para abrir, auditar e copiar; a gravação troca o arquivo inteiro de uma
/// vez para não deixar registro pela metade.
/// </summary>
public static class TaskStore
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "tasks");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(TaskRecord record)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, record.Id + ".json");
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(record, Options));
            File.Move(temp, path, overwrite: true);
        }
        catch { /* sem permissão/disco cheio: a tarefa segue; só não entra no Histórico */ }
    }

    public static List<TaskRecord> LoadAll()
    {
        var list = new List<TaskRecord>();
        if (!Directory.Exists(Folder)) return list;
        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<TaskRecord>(File.ReadAllText(file), Options) is { } r) list.Add(r);
            }
            catch { /* arquivo corrompido: ignora, os outros continuam */ }
        }
        return list.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public static void Delete(string id)
    {
        try { File.Delete(Path.Combine(Folder, id + ".json")); } catch { /* já removido */ }
    }
}
