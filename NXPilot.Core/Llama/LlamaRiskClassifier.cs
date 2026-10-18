// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using NXPilot.Risk;

namespace NXPilot.Llama;

/// <summary>
/// Classificador por LLM gerativo (decoder), reaproveitando a IA Local já instalada pelo
/// NXProject: GGUF + llama.dll da mesma pasta. Saída limitada a poucos tokens — o custo
/// dominante é ler o prompt (prefill), que na CPU leva segundos.
/// </summary>
public sealed class LlamaRiskClassifier(string? folder = null, string? modelPath = null) : IRiskClassifier
{
    /// <summary>
    /// Modelos preferidos, em ordem — todos com licença que permite uso comercial. O Qwen3 é o
    /// mesmo padrão do NXProject (raiz da pasta da IA Local, um arquivo só para os dois apps);
    /// a subpasta "modelos" ficou como alternativa para quem quiser separar.
    /// </summary>
    public static readonly string[] PreferredModels =
    {
        "Qwen3-4B-Instruct-2507-Q4_K_M.gguf",            // Apache-2.0 — padrão do NXProject e do NXPilot
        @"modelos\Qwen3-4B-Instruct-2507-Q4_K_M.gguf",
    };

    /// <summary>Licença conhecida do arquivo de modelo (para a tela avisar quando não for comercial).</summary>
    public static (string License, bool Commercial) LicenseOf(string? file) => file switch
    {
        null => ("?", false),
        _ when file.Contains("Qwen2.5-3B", StringComparison.OrdinalIgnoreCase) || file.Contains("Qwen2.5-72B", StringComparison.OrdinalIgnoreCase)
            => (NxText.T("Qwen Research — somente uso NÃO comercial", "Qwen Research — NON-commercial use only"), false),
        _ when file.Contains("Qwen3", StringComparison.OrdinalIgnoreCase) || file.Contains("Qwen2.5", StringComparison.OrdinalIgnoreCase)
            => ("Apache-2.0", true),
        _ when file.Contains("Phi-4", StringComparison.OrdinalIgnoreCase) => ("MIT", true),
        _ => (NxText.T("desconhecida — confira antes de uso comercial", "unknown — check before commercial use"), false),
    };

    // llama.dll só pode ser apontada uma vez por processo e NUNCA descarregada
    // (FreeLibrary derruba o processo — lição do NXProject).
    private static bool _nativeConfigured;
    private static readonly object NativeLock = new();

    private LLamaWeights? _weights;
    private ModelParams? _params;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Folder { get; } = folder ?? ResolveFolder();

    /// <summary>"CPU", "Cuda12" ou "Vulkan", conforme backend-kind.txt gravado pelo NXProject.</summary>
    public string Backend { get; } = ReadBackendKind(folder ?? ResolveFolder());

    public string Name => $"LLaMA {Backend} ({ModelFile ?? "sem modelo"})";

    /// <summary>
    /// Caminho do modelo: o informado; senão o primeiro preferido (licença comercial) que existir;
    /// senão o maior .gguf da raiz (o mesmo que o NXProject usa).
    /// </summary>
    public string? ModelPath { get; } = modelPath ?? ResolveModel(folder ?? ResolveFolder());

    public string? ModelFile => ModelPath == null ? null : Path.GetFileName(ModelPath);

    public bool IsInstalled => ModelPath != null && File.Exists(ModelPath) && File.Exists(Path.Combine(Folder, "llama.dll"));

    private static string? ResolveModel(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        foreach (var rel in PreferredModels)
            if (File.Exists(Path.Combine(folder, rel))) return Path.Combine(folder, rel);
        return Directory.EnumerateFiles(folder, "*.gguf").OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
    }

    /// <summary>Pasta da IA Local do NXProject (local-ai.json) ou C:\IA_LOCAL.</summary>
    public static string ResolveFolder()
    {
        try
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NXProject.Community", "local-ai.json");
            if (File.Exists(cfg))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(cfg));
                if (doc.RootElement.TryGetProperty("ResourcesFolder", out var f) && f.GetString() is { Length: > 0 } s)
                    return s;
            }
        }
        catch { /* config inválida: usa o padrão */ }
        return @"C:\IA_LOCAL";
    }

    private static string ReadBackendKind(string folder)
    {
        try
        {
            var file = Path.Combine(folder, "backend-kind.txt");
            return File.Exists(file) ? File.ReadAllText(file).Trim() : "CPU";
        }
        catch { return "CPU"; }
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        if (!IsInstalled)
            throw new InvalidOperationException($"IA Local (GGUF + llama.dll) não encontrada em {Folder}.");

        lock (NativeLock)
        {
            if (!_nativeConfigured)
            {
                NativeLibraryConfig.All.WithLibrary(Path.Combine(Folder, "llama.dll"), Path.Combine(Folder, "mtmd.dll"));
                NativeLogConfig.llama_log_set((_, _) => { });
                _nativeConfigured = true;
            }
        }

        var gpu = !Backend.Equals("CPU", StringComparison.OrdinalIgnoreCase);
        _params = new ModelParams(ModelPath!)
        {
            ContextSize = 1024,              // classificação: prompt curto, não precisa de 8k
            GpuLayerCount = gpu ? 999 : 0,
            Threads = Math.Max(1, Environment.ProcessorCount / 2),
            BatchSize = 512
        };
        _weights = LLamaWeights.LoadFromFile(_params);
    }, ct);

    public async Task<RiskResult> ClassifyAsync(string action, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var sw = Stopwatch.StartNew();
            var prompt = new StringBuilder()
                .Append("<|im_start|>system\n").Append(RiskPrompt.System).Append("<|im_end|>\n")
                .Append("<|im_start|>user\nAção: ").Append(action).Append("<|im_end|>\n")
                .Append("<|im_start|>assistant\n")
                .ToString();

            var executor = new StatelessExecutor(_weights!, _params!);
            var inference = new InferenceParams
            {
                MaxTokens = 4,
                AntiPrompts = new List<string> { "<|im_end|>", "\n" },
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0f }
            };

            var sb = new StringBuilder();
            await foreach (var token in executor.InferAsync(prompt, inference, ct))
                sb.Append(token);

            var level = RiskPrompt.Parse(sb.ToString(), out var understood);
            return new RiskResult(level, understood ? 0.8 : 0, Name, NxText.T("resposta: ", "answer: ") + $"\"{sb.ToString().Trim()}\"", sw.Elapsed);
        }
        finally { _gate.Release(); }
    }

    // Os pesos são liberados; a llama.dll permanece carregada de propósito.
    public void Dispose() => _weights?.Dispose();
}
