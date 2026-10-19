// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text.Json;

namespace NXPilot.Risk;

/// <summary>Exemplos ensinados pela pessoa, separados da semente e dos benchmarks.</summary>
public sealed class RiskExampleStore(string? filePath = null)
{
    private static readonly object Sync = new();
    public string FilePath { get; } = Path.GetFullPath(filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "risk-examples.json"));

    public IReadOnlyList<RiskExample> Load()
    {
        lock (Sync) return Read();
    }

    public void Upsert(RiskExample example)
    {
        example = Validate(example);
        lock (Sync)
        {
            var examples = Read().Where(e => e.Action != example.Action).Append(example).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, examples, new JsonSerializerOptions { WriteIndented = true });
                    file.Flush(flushToDisk: true);
                }
                File.Move(temporary, FilePath, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    private IReadOnlyList<RiskExample> Read()
    {
        if (!File.Exists(FilePath)) return Array.Empty<RiskExample>();
        try
        {
            var examples = JsonSerializer.Deserialize<RiskExample[]>(File.ReadAllText(FilePath))
                ?? throw new JsonException("Expected an array of risk examples.");
            return examples.Select(Validate).GroupBy(e => e.Action, StringComparer.Ordinal).Select(g => g.Last()).ToArray();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Não foi possível ler os exemplos ensinados: " + FilePath, ex);
        }
    }

    internal static RiskExample Validate(RiskExample example)
    {
        ArgumentNullException.ThrowIfNull(example);
        ArgumentException.ThrowIfNullOrWhiteSpace(example.Action);
        if (!Enum.IsDefined(example.Level)) throw new ArgumentOutOfRangeException(nameof(example));
        return example with { Action = example.Action.Trim() };
    }
}
