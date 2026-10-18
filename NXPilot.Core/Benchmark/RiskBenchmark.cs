// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using NXPilot.Risk;

namespace NXPilot.Benchmark;

public sealed record BenchmarkCase(RiskExample Expected, RiskResult Actual)
{
    public bool Correct => Expected.Level == Actual.Level;
}

public sealed record BenchmarkReport(string Engine, TimeSpan Load, IReadOnlyList<BenchmarkCase> Cases, string? Error = null)
{
    public int CorrectCount => Cases.Count(c => c.Correct);
    public double AverageMs => Cases.Count == 0 ? 0 : Cases.Average(c => c.Actual.Elapsed.TotalMilliseconds);

    public double P95Ms
    {
        get
        {
            if (Cases.Count == 0) return 0;
            var sorted = Cases.Select(c => c.Actual.Elapsed.TotalMilliseconds).Order().ToList();
            return sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
        }
    }

    /// <summary>Erro perigoso: algo ALTO classificado abaixo (seria aprovado sozinho).</summary>
    public int MissedHigh => Cases.Count(c => c.Expected.Level == RiskLevel.Alto && c.Actual.Level != RiskLevel.Alto);
}

/// <summary>
/// Mede carga (uma vez) e latência por ação. A primeira inferência é aquecimento e fica
/// fora da média — em GPU ela inclui a compilação dos kernels.
/// </summary>
public static class RiskBenchmark
{
    public static async Task<BenchmarkReport> RunAsync(IRiskClassifier classifier, IReadOnlyList<RiskExample>? cases = null,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        cases ??= RiskExamples.Benchmark;
        var sw = Stopwatch.StartNew();
        try
        {
            progress?.Report($"{classifier.Name}: " + NxText.T("carregando…", "loading…"));
            await classifier.InitializeAsync(ct);
            var load = sw.Elapsed;

            await classifier.ClassifyAsync("Bash: echo aquecimento", ct);

            var results = new List<BenchmarkCase>();
            for (var i = 0; i < cases.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"{classifier.Name}: {i + 1}/{cases.Count}");
                results.Add(new BenchmarkCase(cases[i], await classifier.ClassifyAsync(cases[i].Action, ct)));
            }
            return new BenchmarkReport(classifier.Name, load, results);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new BenchmarkReport(classifier.Name, sw.Elapsed, Array.Empty<BenchmarkCase>(), ex.Message);
        }
    }
}
