// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NXPilot.Ai;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Escolha e configuração do LLM do 3º degrau da cascata. Edita uma CÓPIA das configurações
/// e só grava no Salvar; quem abriu recarrega o gate quando <see cref="Window.DialogResult"/> é true.
/// </summary>
public partial class AiProviderWindow : Window
{
    private readonly AiProviderSettings _s;
    private bool _loading = true;

    public AiProviderWindow(AiProviderSettings settings)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        _s = settings;
        foreach (var kind in Enum.GetValues<AiProviderKind>())
            ProviderBox.Items.Add(new ComboBoxItem { Content = AiProviderSettings.DisplayName(kind), Tag = kind });
        ProviderBox.SelectedIndex = (int)_s.Kind;
        EndpointBox.Text = _s.Endpoint;
        ModelBox.Text = _s.Model;
        TenantBox.Text = _s.TenantId;
        ClientBox.Text = _s.ClientId;
        ScopeBox.Text = string.IsNullOrWhiteSpace(_s.Scope) ? AiProviderSettings.DefaultM365Scope : _s.Scope;
        _loading = false;
        ApplyKind(_s.Kind, fillDefaults: false);
        ShowSession();
    }

    private AiProviderKind SelectedKind => (AiProviderKind)((ComboBoxItem)ProviderBox.SelectedItem).Tag;

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ApplyKind(SelectedKind, fillDefaults: true);
    }

    /// <summary>Mostra só os campos do provedor; ao trocar, sugere endpoint/modelo padrão se vazios ou de outro provedor.</summary>
    private void ApplyKind(AiProviderKind kind, bool fillDefaults)
    {
        var remote = kind != AiProviderKind.LocalLlama;
        RemoteFields.Visibility = Vis(remote);
        RemoteWarning.Visibility = Vis(remote);
        TestButton.Visibility = Vis(remote);
        M365Panel.Visibility = Vis(kind == AiProviderKind.Microsoft365);
        KeyPanel.Visibility = Vis(kind != AiProviderKind.Microsoft365);
        ModelPanel.Visibility = Vis(kind is AiProviderKind.GitHubCopilot or AiProviderKind.OpenRouter);

        KeyLabel.Text = kind switch
        {
            AiProviderKind.GitHubCopilot => T("Token do GitHub (fine-grained, permissão \"models\")", "GitHub token (fine-grained, \"models\" permission)", "Token de GitHub (fine-grained, permiso \"models\")"),
            AiProviderKind.AzureOpenAI => T("Chave do Azure OpenAI (header api-key)", "Azure OpenAI key (api-key header)", "Clave de Azure OpenAI (encabezado api-key)"),
            AiProviderKind.OpenRouter => T("Chave do OpenRouter", "OpenRouter key", "Clave de OpenRouter"),
            _ => ""
        };
        KeyState.Text = _s.ApiKey.Length > 0 && kind == _s.Kind ? T("Chave salva (cifrada). Deixe em branco para manter.", "Key saved (encrypted). Leave blank to keep it.", "Clave guardada (cifrada). Déjelo en blanco para mantenerla.") : "";

        ProviderHint.Text = kind switch
        {
            AiProviderKind.LocalLlama => T("Roda na máquina, sem enviar nada. É a IA Local compartilhada com o NXProject.", "Runs on this machine, sends nothing. It is the Local AI shared with NXProject.", "Se ejecuta en esta máquina, no envía nada. Es la IA Local compartida con NXProject."),
            AiProviderKind.GitHubCopilot => T("Usa os modelos do GitHub (GitHub Models) com o token da sua conta Copilot.", "Uses GitHub models (GitHub Models) with your Copilot account token.", "Usa los modelos de GitHub (GitHub Models) con el token de su cuenta de Copilot."),
            AiProviderKind.AzureOpenAI => T("O deployment de Azure OpenAI da empresa: URL completa com api-version.", "Your company Azure OpenAI deployment: full URL with api-version.", "El deployment de Azure OpenAI de su empresa: URL completa con api-version."),
            AiProviderKind.Microsoft365 => T("Azure OpenAI com o login corporativo (Entra ID) — a TI informa Tenant, Client ID e o endpoint.", "Azure OpenAI with corporate sign-in (Entra ID) — IT provides the Tenant, Client ID and endpoint.", "Azure OpenAI con inicio de sesión corporativo (Entra ID) — TI proporciona el Tenant, el Client ID y el endpoint."),
            AiProviderKind.OpenRouter => T("Vários modelos com uma chave só, inclusive gratuitos (openrouter/free).", "Many models with a single key, free ones included (openrouter/free).", "Muchos modelos con una sola clave, incluidos gratuitos (openrouter/free)."),
            _ => ""
        };

        if (fillDefaults)
        {
            var known = Enum.GetValues<AiProviderKind>().Select(AiProviderSettings.DefaultEndpoint).Where(x => x.Length > 0);
            if (EndpointBox.Text.Length == 0 || known.Contains(EndpointBox.Text)) EndpointBox.Text = AiProviderSettings.DefaultEndpoint(kind);
            var models = Enum.GetValues<AiProviderKind>().Select(AiProviderSettings.DefaultModel).Where(x => x.Length > 0);
            if (ModelBox.Text.Length == 0 || models.Contains(ModelBox.Text)) ModelBox.Text = AiProviderSettings.DefaultModel(kind);
        }
    }

    /// <summary>Configuração da tela, sem gravar — usada pelo Testar e pelo Salvar.</summary>
    private AiProviderSettings Collect() => new()
    {
        Kind = SelectedKind,
        Endpoint = EndpointBox.Text.Trim(),
        Model = ModelBox.Text.Trim(),
        TenantId = TenantBox.Text.Trim(),
        ClientId = ClientBox.Text.Trim(),
        Scope = ScopeBox.Text.Trim(),
        ApiKey = KeyBox.Password.Length > 0 ? KeyBox.Password : (SelectedKind == _s.Kind ? _s.ApiKey : ""),
    };

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        Status(T("Testando…", "Testing…", "Probando…"), Brushes.DimGray);
        try
        {
            using var client = new ChatRiskClassifier(Collect());
            await client.InitializeAsync();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = await client.ClassifyAsync("Bash: git push --force origin main");
            Status(T($"Conectado. Classificou um push forçado como {r.Level} em {sw.ElapsedMilliseconds} ms ({r.Reason}).", $"Connected. It classified a forced push as {r.Level} in {sw.ElapsedMilliseconds} ms ({r.Reason}).", $"Conectado. Clasificó un push forzado como {r.Level} en {sw.ElapsedMilliseconds} ms ({r.Reason})."),
                r.Level == Risk.RiskLevel.Alto ? Brushes.Green : Brushes.DarkOrange);
        }
        catch (Exception ex) { Status(ex.Message, Brushes.Firebrick); }
        finally { TestButton.IsEnabled = true; }
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        try
        {
            var prompt = await EntraAuthService.StartLoginAsync(TenantBox.Text, ClientBox.Text, ScopeBox.Text);
            LoginCode.Text = T($"Código: {prompt.UserCode} — digite em {prompt.VerificationUri} (a página já foi aberta no navegador).", $"Code: {prompt.UserCode} — enter it at {prompt.VerificationUri} (the page was opened in your browser).", $"Código: {prompt.UserCode} — ingréselo en {prompt.VerificationUri} (la página se abrió en su navegador).");
            var token = await EntraAuthService.CompleteLoginAsync(prompt, TenantBox.Text, ClientBox.Text, ScopeBox.Text);
            LoginCode.Text = "";
            Status(T($"Conectado como {token.Account}.", $"Signed in as {token.Account}.", $"Sesión iniciada como {token.Account}."), Brushes.Green);
            ShowSession();
        }
        catch (Exception ex) { Status(ex.Message, Brushes.Firebrick); }
        finally { LoginButton.IsEnabled = true; }
    }

    private void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        EntraAuthService.SignOut();
        ShowSession();
    }

    private void ShowSession()
    {
        var s = EntraAuthService.GetCurrentSession();
        SessionText.Text = s is { } x
            ? T($"Conta conectada: {x.Account} · token até {x.ExpiresAtUtc.ToLocalTime():dd/MM HH:mm} (renova sozinho)", $"Signed-in account: {x.Account} · token until {x.ExpiresAtUtc.ToLocalTime():MM/dd HH:mm} (renews automatically)", $"Cuenta conectada: {x.Account} · token hasta {x.ExpiresAtUtc.ToLocalTime():dd/MM HH:mm} (se renueva automáticamente)")
            : T("Nenhuma conta conectada.", "No account signed in.", "Ninguna cuenta conectada.");
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var c = Collect();
        _s.Kind = c.Kind;
        _s.Endpoint = c.Endpoint;
        _s.Model = c.Model;
        _s.TenantId = c.TenantId;
        _s.ClientId = c.ClientId;
        _s.Scope = c.Scope;
        _s.ApiKey = c.ApiKey;
        try
        {
            _s.Save();
            DialogResult = true;
        }
        catch (Exception ex) { Status(T("Não salvou: ", "Not saved: ", "No se guardó: ") + ex.Message, Brushes.Firebrick); }
    }

    private void Status(string text, Brush color)
    {
        StatusText.Text = text;
        StatusText.Foreground = color;
    }

    private static Visibility Vis(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
}
