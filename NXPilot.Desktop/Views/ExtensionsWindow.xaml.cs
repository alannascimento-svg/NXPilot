// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NXPilot.Databricks;
using NXPilot.GitRepos;
using NXPilot.Integrations;
using NXPilot.Mail;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Extensões: Databricks SQL, Git (GitHub/Azure DevOps) e E-mail (Outlook/Gmail). Os testes usam
/// o que está na tela (sem salvar); o Salvar grava as três configurações. Token em branco = mantém.
/// </summary>
public partial class ExtensionsWindow : Window
{
    private readonly DatabricksSettings _db;
    private readonly GitRepoSettings _git;
    private readonly MailSettings _mail;

    public ExtensionsWindow(string? tab = null)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        _db = DatabricksSettings.Load();
        _git = GitRepoSettings.Load();
        _mail = MailSettings.Load();

        DbHostBox.Text = _db.Host;
        DbWarehouseBox.Text = _db.WarehouseId;
        DbCatalogBox.Text = _db.Catalog;
        DbSchemaBox.Text = _db.Schema;
        AdoOrgBox.Text = _git.AzureDevOpsOrganizationUrl;
        OutlookClientBox.Text = _mail.OutlookClientId;
        OutlookTenantBox.Text = _mail.OutlookTenant;
        GmailClientBox.Text = _mail.GmailClientId;

        Tabs.SelectedItem = tab?.ToLowerInvariant() switch
        {
            "git" => GitTab,
            "mail" or "email" => MailTab,
            _ => DatabricksTab,
        };
        Refresh();
    }

    // ------------------------------------------------------------ estado

    private void Refresh()
    {
        string Saved(string value) => value.Length > 0 ? T("Salvo (cifrado). Deixe em branco para manter.", "Saved (encrypted). Leave blank to keep it.") : "";
        DbTokenState.Text = Saved(_db.Token);
        GhTokenState.Text = Saved(_git.GitHubToken);
        AdoPatState.Text = _git.AzureDevOpsFromNXProject
            ? T("Usando o PAT do NXProject (não é copiado para o NXPilot). Ele precisa do escopo Code para repositórios.",
                "Using the NXProject PAT (not copied into NXPilot). It needs the Code scope for repositories.")
            : Saved(_git.AzureDevOpsToken);
        GmailSecretState.Text = Saved(_mail.GmailClientSecret);

        Dot(DatabricksDot, _db.ToConnection().IsComplete);
        Dot(GitDot, _git.GitHubToken.Length > 0 || _git.AzureDevOpsToken.Length > 0);
        var outlook = Session(MailProvider.Outlook);
        var gmail = Session(MailProvider.Gmail);
        OutlookSession.Text = outlook.Text;
        GmailSession.Text = gmail.Text;
        Dot(MailDot, outlook.SignedIn || gmail.SignedIn);
    }

    private static (bool SignedIn, string Text) Session(MailProvider provider)
    {
        var token = new DpapiTokenStore(provider).LoadAsync().GetAwaiter().GetResult();
        return token == null
            ? (false, T("Não conectado.", "Not signed in."))
            : (true, T($"Conectado · renova sozinho{(token.RefreshToken == null ? $" (expira {token.ExpiresAt.LocalDateTime:dd/MM HH:mm})" : "")}",
                       $"Signed in · renews automatically{(token.RefreshToken == null ? $" (expires {token.ExpiresAt.LocalDateTime:MM/dd HH:mm})" : "")}"));
    }

    private static void Dot(Ellipse dot, bool on)
    {
        dot.Fill = on ? Brushes.SeaGreen : Brushes.Silver;
        dot.ToolTip = on ? T("Configurada", "Configured") : T("Não configurada", "Not configured");
    }

    // ------------------------------------------------------------ o que está na tela

    private DatabricksSettings CollectDatabricks() => new()
    {
        Host = DbHostBox.Text.Trim(),
        WarehouseId = DbWarehouseBox.Text.Trim(),
        Catalog = DbCatalogBox.Text.Trim(),
        Schema = DbSchemaBox.Text.Trim(),
        Token = DbTokenBox.Password.Length > 0 ? DbTokenBox.Password : _db.Token,
    };

    private GitRepoSettings CollectGit() => new()
    {
        AzureDevOpsOrganizationUrl = AdoOrgBox.Text.Trim(),
        GitHubToken = GhTokenBox.Password.Length > 0 ? GhTokenBox.Password : _git.GitHubToken,
        AzureDevOpsToken = AdoPatBox.Password.Length > 0 ? AdoPatBox.Password : _git.AzureDevOpsToken,
        AzureDevOpsFromNXProject = AdoPatBox.Password.Length == 0 && _git.AzureDevOpsFromNXProject,
    };

    private MailSettings CollectMail() => new()
    {
        OutlookClientId = OutlookClientBox.Text.Trim(),
        OutlookTenant = OutlookTenantBox.Text.Trim() is { Length: > 0 } t ? t : "common",
        GmailClientId = GmailClientBox.Text.Trim(),
        GmailClientSecret = GmailSecretBox.Password.Length > 0 ? GmailSecretBox.Password : _mail.GmailClientSecret,
    };

    // ------------------------------------------------------------ Databricks

    private async void OnDatabricksTestClick(object sender, RoutedEventArgs e) => await Run(DbTestButton, DbStatus, async () =>
    {
        var conn = CollectDatabricks().ToConnection();
        if (!conn.IsComplete) return (T("Preencha host, warehouse e token.", "Fill in host, warehouse and token."), Brushes.Firebrick);
        Status(DbStatus, T("Conectando… (warehouse parado pode levar alguns minutos para ligar)", "Connecting… (a stopped warehouse may take a few minutes to start)"), Brushes.DimGray);
        using var svc = new DatabricksSqlService(conn);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await svc.TestConnectionAsync();
        var catalogs = await svc.ListCatalogsAsync();
        return (T($"Conectado em {sw.Elapsed.TotalSeconds:F1}s. Catálogos: {string.Join(", ", catalogs.Take(8))}{(catalogs.Count > 8 ? "…" : "")}",
                  $"Connected in {sw.Elapsed.TotalSeconds:F1}s. Catalogs: {string.Join(", ", catalogs.Take(8))}{(catalogs.Count > 8 ? "…" : "")}"), Brushes.Green);
    });

    // ------------------------------------------------------------ Git

    private async void OnGitHubTestClick(object sender, RoutedEventArgs e) => await TestGit(GhTestButton, GitProvider.GitHub);
    private async void OnDevOpsTestClick(object sender, RoutedEventArgs e) => await TestGit(AdoTestButton, GitProvider.AzureDevOps);

    private Task TestGit(Button button, GitProvider provider) => Run(button, GitStatus, async () =>
    {
        var s = CollectGit();
        if (s.For(provider) == null) return (T($"Informe o token do {provider}.", $"Enter the {provider} token."), Brushes.Firebrick);
        var repos = await new GitRepoService(s).ListRepositoriesAsync(provider, s.AzureDevOpsOrganizationUrl);
        var names = string.Join(", ", repos.Take(6).Select(r => r.ToString()));
        return (T($"{provider}: {repos.Count} repositório(s). {names}{(repos.Count > 6 ? "…" : "")}",
                  $"{provider}: {repos.Count} repositor{(repos.Count == 1 ? "y" : "ies")}. {names}{(repos.Count > 6 ? "…" : "")}"), Brushes.Green);
    });

    // ------------------------------------------------------------ E-mail

    private async void OnOutlookLoginClick(object sender, RoutedEventArgs e) => await Login(OutlookLoginButton, MailProvider.Outlook);
    private async void OnGmailLoginClick(object sender, RoutedEventArgs e) => await Login(GmailLoginButton, MailProvider.Gmail);
    private void OnOutlookSignOutClick(object sender, RoutedEventArgs e) { new DpapiTokenStore(MailProvider.Outlook).Delete(); Refresh(); }
    private void OnGmailSignOutClick(object sender, RoutedEventArgs e) { new DpapiTokenStore(MailProvider.Gmail).Delete(); Refresh(); }
    private async void OnOutlookTestClick(object sender, RoutedEventArgs e) => await TestMail(OutlookTestButton, MailProvider.Outlook);
    private async void OnGmailTestClick(object sender, RoutedEventArgs e) => await TestMail(GmailTestButton, MailProvider.Gmail);

    private Task Login(Button button, MailProvider provider) => RunMail(button, async () =>
    {
        var tokens = CollectMail().Tokens(provider);
        if (tokens == null)
            return (provider == MailProvider.Outlook
                ? T("Informe o Client ID do app no Entra ID.", "Enter the Entra ID app Client ID.")
                : T("Informe o Client ID e o Client secret do Google Cloud.", "Enter the Google Cloud Client ID and Client secret."), Brushes.Firebrick);
        Status(MailStatus, T("Conclua o login no navegador que foi aberto…", "Finish signing in in the browser that just opened…"), Brushes.DimGray);
        await tokens.LoginAsync();
        Refresh();
        return (T($"{provider}: login concluído (somente leitura).", $"{provider}: signed in (read-only)."), Brushes.Green);
    });

    private Task TestMail(Button button, MailProvider provider) => RunMail(button, async () =>
    {
        var tokens = CollectMail().Tokens(provider);
        if (tokens == null) return (T("Configure e entre primeiro.", "Configure and sign in first."), Brushes.Firebrick);
        using var mail = MailService.Create(provider, tokens);
        var list = await mail.ListInboxAsync(new MailQuery { Max = 5 });
        var lines = list.Select(m => $"{(m.IsRead ? "  " : "• ")}{m.ReceivedAt.LocalDateTime:dd/MM HH:mm}  {m.From}  —  {m.Subject}");
        return ($"{provider}: {list.Count}\n{string.Join('\n', lines)}", Brushes.Black);
    });

    // ------------------------------------------------------------ salvar

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CollectDatabricks().Save();
            CollectGit().Save();
            CollectMail().Save();
            DialogResult = true;
        }
        catch (Exception ex) { Status(SaveStatus, T("Não salvou: ", "Not saved: ") + ex.Message, Brushes.Firebrick); }
    }

    // ------------------------------------------------------------ apoio

    private async Task Run(Button button, TextBlock status, Func<Task<(string Text, Brush Color)>> action)
    {
        button.IsEnabled = false;
        try { var (text, color) = await action(); Status(status, text, color); }
        catch (Exception ex) { Status(status, ex.Message, Brushes.Firebrick); }
        finally { button.IsEnabled = true; }
    }

    private async Task RunMail(Button button, Func<Task<(string Text, Brush Color)>> action)
    {
        button.IsEnabled = false;
        try { var (text, color) = await action(); Status(MailStatus, text, color); }
        catch (Exception ex) { Status(MailStatus, ex.Message, Brushes.Firebrick); }
        finally { button.IsEnabled = true; }
    }

    private static void Status(TextBlock target, string text, Brush color) { target.Text = text; target.Foreground = color; }
    private static void Status(TextBox target, string text, Brush color) { target.Text = text; target.Foreground = color; }
}
