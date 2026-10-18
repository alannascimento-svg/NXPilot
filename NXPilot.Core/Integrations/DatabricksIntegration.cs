// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using NXPilot.Ai;
using NXPilot.Databricks;
using NXPilot.Risk;

namespace NXPilot.Integrations;

/// <summary>
/// Conexão Databricks do NXPilot: host, warehouse, catálogo/schema padrão e o token cifrado
/// com DPAPI (perfil do usuário) em %LOCALAPPDATA%\NXPilot\databricks.json.
/// </summary>
public sealed class DatabricksSettings
{
    public string Host { get; set; } = "";
    public string WarehouseId { get; set; } = "";
    public string Catalog { get; set; } = "";
    public string Schema { get; set; } = "";

    [JsonIgnore] public string Token { get; set; } = "";
    public string TokenProtected { get; set; } = "";

    public DatabricksConnection ToConnection() => new(Host, Token, WarehouseId, Catalog, Schema);

    internal static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "databricks.json");

    /// <summary>Configuração salva; sem arquivo, cai nas variáveis DATABRICKS_* (útil no Bench).</summary>
    [SupportedOSPlatform("windows")]
    public static DatabricksConnection? LoadConnection()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<DatabricksSettings>(File.ReadAllText(FilePath)) is { } s)
            {
                s.Token = Secret.Unprotect(s.TokenProtected);
                if (s.ToConnection() is { IsComplete: true } c) return c;
            }
        }
        catch { /* arquivo inválido */ }
        return DatabricksConnection.FromEnvironment();
    }

    [SupportedOSPlatform("windows")]
    public void Save()
    {
        TokenProtected = Secret.Protect(Token);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>
/// Liga o pacote NXPilot.Databricks ao DecisionGate: leitura é BAIXO (passa sozinha); sessão e
/// SQL desconhecido são MÉDIO; escrita, DDL e permissões são ALTO. O que não for aprovado sozinho
/// vai para <paramref name="askUser"/> — sem ele, nega (nunca libera por omissão).
/// </summary>
public sealed class GateSqlApprover(Func<SqlStatementReview, RiskResult, CancellationToken, Task<bool>>? askUser = null)
    : ISqlStatementApprover
{
    public static RiskResult Risk(SqlStatementReview review) => review.Kind switch
    {
        SqlStatementKind.Read or SqlStatementKind.Empty => new(RiskLevel.Baixo, 0.95, "SQL", "leitura", TimeSpan.Zero),
        SqlStatementKind.Session => new(RiskLevel.Medio, 0.9, "SQL", "altera a sessão (USE/SET)", TimeSpan.Zero),
        SqlStatementKind.Unknown => new(RiskLevel.Medio, 0.5, "SQL", "instrução não reconhecida", TimeSpan.Zero),
        _ => new(RiskLevel.Alto, 0.95, "SQL", review.IsDestructive ? $"{review.Kind}, destrutivo" : review.Kind.ToString(), TimeSpan.Zero),
    };

    public async Task<SqlApproval> ApproveAsync(SqlStatementReview review, CancellationToken ct = default)
    {
        var risk = Risk(review);
        if (DecisionGate.Decide(risk) == GateOutcome.AutoApprove) return SqlApproval.Allow($"gate: {risk.Level}");
        if (askUser == null) return SqlApproval.Deny($"gate: {risk.Level} — ninguém para aprovar");
        return await askUser(review, risk, ct)
            ? SqlApproval.Allow($"usuário aprovou ({risk.Level})")
            : SqlApproval.Deny($"usuário negou ({risk.Level})");
    }
}
