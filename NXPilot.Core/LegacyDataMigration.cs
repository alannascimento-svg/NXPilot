// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot;

/// <summary>
/// O produto público se chamava NXAgentic: configurações, histórico de tarefas, tutorial,
/// segredos (DPAPI) e worktrees ficavam em %LOCALAPPDATA%\NXAgentic. No início do NXPilot,
/// cada item que ainda não existe em %LOCALAPPDATA%\NXPilot é MOVIDO para lá — nada é
/// sobrescrito; o que já existir dos dois lados fica onde está (a pasta antiga só some vazia).
/// </summary>
public static class LegacyDataMigration
{
    public static string OldFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXAgentic");
    public static string NewFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot");

    /// <returns>Quantos itens foram movidos.</returns>
    public static int Run(string? oldFolder = null, string? newFolder = null)
    {
        var from = oldFolder ?? OldFolder;
        var to = newFolder ?? NewFolder;
        if (!Directory.Exists(from)) return 0;
        var moved = 0;
        try { moved = Merge(from, to); }
        catch { /* em uso: o que não moveu fica na pasta antiga e tenta de novo no próximo início */ }
        try { if (!Directory.EnumerateFileSystemEntries(from).Any()) Directory.Delete(from); } catch { }
        return moved;
    }

    private static int Merge(string from, string to)
    {
        Directory.CreateDirectory(to);
        var moved = 0;
        foreach (var file in Directory.GetFiles(from))
        {
            var target = Path.Combine(to, Path.GetFileName(file));
            if (File.Exists(target)) continue; // o novo vence: nunca sobrescreve
            File.Move(file, target);
            moved++;
        }
        foreach (var dir in Directory.GetDirectories(from))
        {
            var target = Path.Combine(to, Path.GetFileName(dir));
            if (!Directory.Exists(target)) { Directory.Move(dir, target); moved++; continue; }
            moved += Merge(dir, target); // pasta dos dois lados (ex.: worktrees vazia criada antes): mescla
            if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }
        return moved;
    }
}
