// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using System.Net.Http;

namespace NXPilot.Onnx;

/// <summary>
/// Pasta do modelo ONNX de embeddings (all-MiniLM-L6-v2, ~90 MB). Fica ao lado da IA Local
/// do NXProject (C:\IA_LOCAL), em C:\IA_LOCAL\ONNX\&lt;modelo&gt;; baixado do Hugging Face sob demanda.
/// </summary>
public sealed class OnnxModelStore(string? root = null)
{
    public const string DefaultRoot = @"C:\IA_LOCAL\ONNX";
    public const string ModelName = "all-MiniLM-L6-v2";
    private const string BaseUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/";

    public string Folder { get; } = Path.Combine(root ?? DefaultRoot, ModelName);
    public string ModelPath => Path.Combine(Folder, "model.onnx");
    public string VocabPath => Path.Combine(Folder, "vocab.txt");

    public bool IsInstalled => File.Exists(ModelPath) && File.Exists(VocabPath);

    /// <summary>Baixa vocab + modelo (progresso em MB); grava em .part e renomeia no fim.</summary>
    public async Task DownloadAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Folder);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        await DownloadFileAsync(http, BaseUrl + "vocab.txt", VocabPath, progress, ct);
        await DownloadFileAsync(http, BaseUrl + "onnx/model.onnx", ModelPath, progress, ct);
        progress?.Report($"Modelo ONNX pronto em {Folder}");
    }

    private static async Task DownloadFileAsync(HttpClient http, string url, string target, IProgress<string>? progress, CancellationToken ct)
    {
        if (File.Exists(target)) return;
        var name = Path.GetFileName(target);
        var part = target + ".part";
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(part))
        {
            var buffer = new byte[1 << 16];
            long done = 0, lastMb = -1;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                done += n;
                var mb = done / (1024 * 1024);
                if (mb != lastMb)
                {
                    lastMb = mb;
                    progress?.Report(total is > 0
                        ? $"Baixando {name}: {mb} / {total.Value / (1024 * 1024)} MB"
                        : $"Baixando {name}: {mb} MB");
                }
            }
        }
        File.Move(part, target, overwrite: true);
    }
}
