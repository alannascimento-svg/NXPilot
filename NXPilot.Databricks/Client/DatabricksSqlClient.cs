// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXPilot.Databricks;

/// <summary>
/// Camada baixa: as 4 chamadas da SQL Statement Execution API (/api/2.0/sql/statements),
/// uma para uma, sem regra nenhuma. Quem orquestra (esperar, paginar, aprovar, cancelar) é o
/// <see cref="DatabricksSqlService"/>. O handler pode ser trocado nos testes.
/// </summary>
public sealed class DatabricksSqlClient : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    private readonly HttpClient _http;
    public DatabricksConnection Connection { get; }

    public DatabricksSqlClient(DatabricksConnection connection, HttpMessageHandler? handler = null)
    {
        if (!connection.IsComplete) throw new ArgumentException("Conexão incompleta: informe host, token e warehouse.", nameof(connection));
        Connection = connection;
        _http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.BaseAddress = new Uri(connection.Host + "/");
        _http.Timeout = TimeSpan.FromSeconds(100); // wait_timeout máximo é 50 s
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NXPilot.Databricks", typeof(DatabricksSqlClient).Assembly.GetName().Version?.ToString(3) ?? "0"));
    }

    /// <summary>POST /api/2.0/sql/statements — espera até <paramref name="waitSeconds"/> (0 ou 5–50) e segue em segundo plano.</summary>
    public Task<StatementResponse> ExecuteAsync(string statement, IReadOnlyList<SqlParameter>? parameters, int rowLimit,
        int waitSeconds = 30, string? catalog = null, string? schema = null, CancellationToken ct = default)
    {
        var body = new ExecuteRequest(statement, Connection.WarehouseId, catalog ?? Connection.Catalog, schema ?? Connection.Schema,
            parameters?.Select(p => new ParameterDto(p.Name, p.Value, p.Type)).ToList(),
            $"{waitSeconds}s", "CONTINUE", "INLINE", "JSON_ARRAY", rowLimit > 0 ? rowLimit : null);
        return SendAsync(HttpMethod.Post, "api/2.0/sql/statements", body, ct);
    }

    /// <summary>GET /api/2.0/sql/statements/{id} — estado, manifesto e o 1º bloco quando SUCCEEDED.</summary>
    public Task<StatementResponse> GetAsync(string statementId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, $"api/2.0/sql/statements/{Uri.EscapeDataString(statementId)}", null, ct);

    /// <summary>GET …/result/chunks/{n} — os blocos seguintes do resultado.</summary>
    public async Task<ResultData> GetChunkAsync(string statementId, int chunkIndex, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/2.0/sql/statements/{Uri.EscapeDataString(statementId)}/result/chunks/{chunkIndex}", ct);
        await EnsureOk(resp, ct);
        return await resp.Content.ReadFromJsonAsync<ResultData>(Json, ct) ?? new ResultData();
    }

    /// <summary>POST …/cancel — resposta vazia; o estado CANCELED aparece no próximo GET.</summary>
    public async Task CancelAsync(string statementId, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync($"api/2.0/sql/statements/{Uri.EscapeDataString(statementId)}/cancel", null, ct);
        await EnsureOk(resp, ct);
    }

    private async Task<StatementResponse> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body != null) req.Content = JsonContent.Create(body, options: Json);
        using var resp = await _http.SendAsync(req, ct);
        await EnsureOk(resp, ct);
        return await resp.Content.ReadFromJsonAsync<StatementResponse>(Json, ct)
               ?? throw new DatabricksException("Resposta vazia do Databricks.");
    }

    /// <summary>Erro HTTP vira DatabricksException com error_code/message do corpo (sem o token).</summary>
    private static async Task EnsureOk(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var text = await resp.Content.ReadAsStringAsync(ct);
        string? code = null, message = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error_code", out var c)) code = c.GetString();
            if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
        }
        catch (JsonException) { /* corpo não é JSON (proxy, HTML de login…) */ }
        var status = (int)resp.StatusCode;
        var hint = status switch
        {
            401 => " Token inválido ou expirado.",
            403 => " Sem permissão no workspace/warehouse.",
            404 => " Warehouse ou instrução não encontrados.",
            _ => ""
        };
        throw new DatabricksException($"Databricks HTTP {status}{(code != null ? $" {code}" : "")}: {message ?? Trim(text)}{hint}", code, null, status);
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] + "…" : s;

    public void Dispose() => _http.Dispose();

    // ------------------------------------------------------------ JSON da API (snake_case)

    private sealed record ExecuteRequest(string Statement, string WarehouseId, string? Catalog, string? Schema,
        List<ParameterDto>? Parameters, string WaitTimeout, string OnWaitTimeout, string Disposition, string Format, long? RowLimit);

    private sealed record ParameterDto(string Name, string? Value, string? Type);
}

public enum StatementState { Pending, Running, Succeeded, Failed, Canceled, Closed }

public sealed class StatementResponse
{
    public string StatementId { get; set; } = "";
    public StatementStatus Status { get; set; } = new();
    public ResultManifest? Manifest { get; set; }
    public ResultData? Result { get; set; }
}

public sealed class StatementStatus
{
    public StatementState State { get; set; }
    public ServiceError? Error { get; set; }
    public string? SqlState { get; set; }
}

public sealed class ServiceError
{
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

public sealed class ResultManifest
{
    public string? Format { get; set; }
    public ResultSchema? Schema { get; set; }
    public int TotalChunkCount { get; set; }
    public long TotalRowCount { get; set; }
    public bool Truncated { get; set; }
}

public sealed class ResultSchema
{
    public int ColumnCount { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new();
}

public sealed class ColumnInfo
{
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public string? TypeName { get; set; }
    public string? TypeText { get; set; }
}

public sealed class ResultData
{
    public int ChunkIndex { get; set; }
    public long RowOffset { get; set; }
    public long RowCount { get; set; }
    public List<List<string?>>? DataArray { get; set; }
    public int? NextChunkIndex { get; set; }
}
