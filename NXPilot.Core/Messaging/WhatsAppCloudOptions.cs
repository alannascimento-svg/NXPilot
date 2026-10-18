// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NXPilot.Messaging;

/// <summary>
/// Configuração do número-robô (WhatsApp Business Platform / Cloud API).
/// <see cref="ApiVersion"/> é configurável porque a Meta lança uma versão da Graph API a cada
/// poucos meses e aposenta as antigas: trocar a versão é editar um campo, não recompilar.
/// </summary>
public sealed class WhatsAppCloudOptions
{
    public const string DefaultApiVersion = "v26.0";

    public string PhoneNumberId { get; set; } = "";
    public string ApiVersion { get; set; } = DefaultApiVersion;
    public int ListenPort { get; set; } = 7071;

    /// <summary>Token que a Meta devolve na verificação do webhook — gerado aqui, colado no painel da Meta.</summary>
    public string VerifyToken { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>Remetentes prioritários (ex.: Diretor) — números no formato wa_id: 55 + DDD + número.</summary>
    public List<string> PrioritySenders { get; set; } = new();
    public List<string> Keywords { get; set; } = new() { "falha", "parada", "laudo", "urgente", "manutenção" };

    // Segredos: só existem decifrados em memória; no disco ficam em DPAPI (perfil do usuário).
    [System.Text.Json.Serialization.JsonIgnore] public string AccessToken { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string AppSecret { get; set; } = "";
    public string AccessTokenProtected { get; set; } = "";
    public string AppSecretProtected { get; set; } = "";

    public bool IsConfigured =>
        PhoneNumberId.Length > 0 && AccessToken.Length > 0 && AppSecret.Length > 0;

    public string LocalWebhookUrl => $"http://localhost:{ListenPort}/whatsapp/";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "whatsapp.json");

    [SupportedOSPlatform("windows")]
    public static WhatsAppCloudOptions Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<WhatsAppCloudOptions>(File.ReadAllText(FilePath)) is { } o)
            {
                o.AccessToken = Unprotect(o.AccessTokenProtected);
                o.AppSecret = Unprotect(o.AppSecretProtected);
                return o;
            }
        }
        catch { /* arquivo inválido: recomeça a configuração */ }
        return new WhatsAppCloudOptions();
    }

    [SupportedOSPlatform("windows")]
    public void Save()
    {
        AccessTokenProtected = Protect(AccessToken);
        AppSecretProtected = Protect(AppSecret);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    [SupportedOSPlatform("windows")]
    private static string Protect(string value) => value.Length == 0 ? "" :
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));

    [SupportedOSPlatform("windows")]
    private static string Unprotect(string value)
    {
        if (value.Length == 0) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); }
        catch { return ""; } // outro usuário/máquina: pede o segredo de novo
    }
}
