// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXPilot.Ai;

/// <summary>
/// Quem responde no 3º degrau da cascata (só nas ações em que o ONNX fica em dúvida).
/// Mesmos provedores do NXProject: GitHub Copilot, Azure OpenAI, Microsoft 365 e OpenRouter.
/// </summary>
public enum AiProviderKind
{
    LocalLlama,     // IA Local (LLaMA) — nada sai da máquina
    GitHubCopilot,  // GitHub Copilot / GitHub Models — token do GitHub
    AzureOpenAI,    // Azure OpenAI da empresa — chave "api-key" + URL do deployment
    Microsoft365,   // conta da empresa (Entra ID) — Azure OpenAI com login corporativo
    OpenRouter,     // OpenRouter — vários modelos (inclusive gratuitos) com uma chave só
}

/// <summary>Configuração do provedor; a chave fica cifrada (DPAPI) no perfil do usuário.</summary>
public sealed class AiProviderSettings
{
    public AiProviderKind Kind { get; set; } = AiProviderKind.LocalLlama;
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Scope { get; set; } = "";

    [JsonIgnore] public string ApiKey { get; set; } = "";
    public string ApiKeyProtected { get; set; } = "";

    public bool IsRemote => Kind != AiProviderKind.LocalLlama;

    /// <summary>
    /// Endpoint padrão por provedor. GitHub: GitHub Models (endpoint de inferência oficial,
    /// OpenAI-compatível, token do GitHub com permissão "models"). O NXProject usa
    /// api.githubcopilot.com — que serve, mas é a API interna dos editores; o campo é editável.
    /// </summary>
    public static string DefaultEndpoint(AiProviderKind kind) => kind switch
    {
        AiProviderKind.GitHubCopilot => "https://models.github.ai/inference/chat/completions",
        AiProviderKind.OpenRouter => "https://openrouter.ai/api/v1/chat/completions",
        _ => "" // Azure / M365: URL do deployment da empresa (…/chat/completions?api-version=…)
    };

    public static string DefaultModel(AiProviderKind kind) => kind switch
    {
        AiProviderKind.GitHubCopilot => "openai/gpt-4.1-mini",
        AiProviderKind.OpenRouter => "openrouter/free", // mesmo padrão do NXProject
        _ => ""
    };

    /// <summary>Escopo do token corporativo para Azure OpenAI (Cognitive Services).</summary>
    public const string DefaultM365Scope = "https://cognitiveservices.azure.com/.default";

    public static string DisplayName(AiProviderKind kind) => kind switch
    {
        AiProviderKind.LocalLlama => NxText.T("IA Local (LLaMA)", "Local AI (LLaMA)", "IA local (LLaMA)"),
        AiProviderKind.GitHubCopilot => "GitHub Copilot",
        AiProviderKind.AzureOpenAI => "Azure OpenAI",
        AiProviderKind.Microsoft365 => NxText.T("Microsoft 365 (conta da empresa)", "Microsoft 365 (company account)", "Microsoft 365 (cuenta de la empresa)"),
        AiProviderKind.OpenRouter => "OpenRouter",
        _ => kind.ToString()
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "ai-provider.json");

    [SupportedOSPlatform("windows")]
    public static AiProviderSettings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<AiProviderSettings>(File.ReadAllText(FilePath)) is { } s)
            {
                s.ApiKey = Secret.Unprotect(s.ApiKeyProtected);
                return s;
            }
        }
        catch { /* arquivo inválido: volta ao LLaMA local */ }
        return new AiProviderSettings();
    }

    [SupportedOSPlatform("windows")]
    public void Save()
    {
        ApiKeyProtected = Secret.Protect(ApiKey);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>DPAPI no escopo do usuário atual — mesma proteção do PAT do NXProject.</summary>
[SupportedOSPlatform("windows")]
public static class Secret
{
    public static string Protect(string value) => value.Length == 0 ? "" :
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));

    public static string Unprotect(string value)
    {
        if (value.Length == 0) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); }
        catch { return ""; } // outro usuário/máquina: pede de novo
    }
}
