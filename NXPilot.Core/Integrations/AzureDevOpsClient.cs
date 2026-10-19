// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NXPilot.Integrations;

public sealed record WorkItem(int Id, string Type, string Title, string State, string Description, string AcceptanceCriteria, string Url);

/// <summary>Conexão Azure DevOps/TFS: organização, projeto e PAT (decifrado só em memória).</summary>
public sealed record DevOpsConnection(string OrganizationUrl, string Project, string Pat)
{
    /// <summary>
    /// Reaproveita a conexão que o NXProject já tem (config_nxproject.json, PAT em DPAPI do
    /// usuário). Somente LEITURA: o NXPilot nunca grava no arquivo do NXProject.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static DevOpsConnection? FromNXProject()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXProject.Community", "config_nxproject.json");
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            string? Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var org = Str("OrganizationUrl");
            var project = Str("TeamProject");
            var enc = Str("EncryptedToken");
            if (string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(enc)) return null;
            var pat = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(enc), null, DataProtectionScope.CurrentUser));
            return pat.Length == 0 ? null : new DevOpsConnection(org.TrimEnd('/'), project, pat);
        }
        catch { return null; } // outro usuário/máquina ou config inválida
    }
}

/// <summary>
/// Leitura de work items pela REST API do Azure DevOps (WIQL + workitems, api-version 7.1).
/// Só GET/consulta — o NXPilot não altera nada no DevOps nesta versão.
/// </summary>
public sealed class AzureDevOpsClient(DevOpsConnection connection) : IDisposable
{
    private readonly HttpClient _http = CreateClient(connection.Pat);

    private static HttpClient CreateClient(string pat)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + pat)));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    /// <summary>Itens atribuídos a mim e ainda abertos (Story, Bug, Task), mais recentes primeiro.</summary>
    public async Task<IReadOnlyList<WorkItem>> MyOpenWorkItemsAsync(int top = 50, CancellationToken ct = default)
    {
        const string wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.AssignedTo] = @Me " +
                            "AND [System.State] NOT IN ('Closed','Done','Removed','Resolved','Concluído','Fechado','Removido') " +
                            "AND [System.WorkItemType] IN ('User Story','Product Backlog Item','Bug','Task','Requirement') ORDER BY [System.ChangedDate] DESC";
        var project = Uri.EscapeDataString(connection.Project);
        using var wiqlResp = await _http.PostAsync($"{connection.OrganizationUrl}/{project}/_apis/wit/wiql?$top={top}&api-version=7.1",
            new StringContent(JsonSerializer.Serialize(new { query = wiql }), Encoding.UTF8, "application/json"), ct);
        await EnsureOk(wiqlResp, ct);
        using var wiqlDoc = JsonDocument.Parse(await wiqlResp.Content.ReadAsStringAsync(ct));
        var ids = wiqlDoc.RootElement.GetProperty("workItems").EnumerateArray().Select(w => w.GetProperty("id").GetInt32()).Take(top).ToList();
        if (ids.Count == 0) return Array.Empty<WorkItem>();

        const string fields = "System.Id,System.WorkItemType,System.Title,System.State,System.Description,Microsoft.VSTS.Common.AcceptanceCriteria";
        using var itemsResp = await _http.GetAsync($"{connection.OrganizationUrl}/_apis/wit/workitems?ids={string.Join(',', ids)}&fields={fields}&api-version=7.1", ct);
        await EnsureOk(itemsResp, ct);
        using var doc = JsonDocument.Parse(await itemsResp.Content.ReadAsStringAsync(ct));
        var byId = new Dictionary<int, WorkItem>();
        foreach (var w in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var f = w.GetProperty("fields");
            string F(string name) => f.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var id = w.GetProperty("id").GetInt32();
            byId[id] = new WorkItem(id, F("System.WorkItemType"), F("System.Title"), F("System.State"),
                PlainText(F("System.Description")), PlainText(F("Microsoft.VSTS.Common.AcceptanceCriteria")),
                $"{connection.OrganizationUrl}/{project}/_workitems/edit/{id}");
        }
        return ids.Where(byId.ContainsKey).Select(i => byId[i]).ToList(); // mantém a ordem do WIQL
    }

    private static async Task EnsureOk(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NonAuthoritativeInformation
            ? NxText.T("O Azure DevOps recusou o token (expirado ou sem permissão de Work Items). Atualize o PAT no NXProject.", "Azure DevOps rejected the token (expired or without Work Items permission). Update the PAT in NXProject.", "Azure DevOps rechazó el token (vencido o sin permiso de Work Items). Actualice el PAT en NXProject.")
            : $"Azure DevOps {(int)resp.StatusCode}: {(body.Length > 200 ? body[..200] : body)}");
    }

    /// <summary>Descrição/critérios vêm em HTML: vira texto simples para o prompt do agente.</summary>
    public static string PlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = Regex.Replace(html, @"<(br|/p|/div|/li)\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<li[^>]*>", "• ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
    }

    public void Dispose() => _http.Dispose();
}
