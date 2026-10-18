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
    private readonly Services.WindowsUiSettings _windows;

    public ExtensionsWindow(string? tab = null)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        _db = DatabricksSettings.Load();
        _git = GitRepoSettings.Load();
        _mail = MailSettings.Load();
        _windows = Services.WindowsUiSettings.Load();
        AllowedAppsBox.Text = string.Join(Environment.NewLine, _windows.AllowedProcesses);

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
            "powerbi" => PowerBiTab,
            "windows" or "janelas" => WindowsTab,
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

        var pbi = Services.PowerBiDesktop.Locate();
        PowerBiLocation.Text = pbi != null
            ? T($"Power BI Desktop encontrado: {pbi}", $"Power BI Desktop found: {pbi}")
            : T("Power BI Desktop não encontrado. Instale pela Microsoft Store ou em powerbi.microsoft.com/desktop.", "Power BI Desktop not found. Install it from the Microsoft Store or powerbi.microsoft.com/desktop.");
        Dot(PowerBiDot, pbi != null);
        Dot(WindowsDot, _windows.AllowedProcesses.Count > 0);
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

    // ------------------------------------------------------------ Power BI Desktop

    private async void OnPowerBiTestClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Power BI Project (*.pbip)|*.pbip", Title = T("Projeto Power BI", "Power BI project") };
        if (dialog.ShowDialog(this) != true) return;
        PowerBiTestButton.IsEnabled = false;
        Status(PowerBiStatus, T("Abrindo no Power BI Desktop (pode levar alguns minutos)…", "Opening in Power BI Desktop (may take a few minutes)…"), Brushes.DimGray);
        try
        {
            var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nxpilot-powerbi-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            await Services.PowerBiDesktop.OpenAndCaptureAsync(dialog.FileName, png);
            Status(PowerBiStatus, T($"Capturado: {png}", $"Captured: {png}"), Brushes.Green);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(png) { UseShellExecute = true });
        }
        catch (Exception ex) { Status(PowerBiStatus, ex.Message, Brushes.Firebrick); }
        finally { PowerBiTestButton.IsEnabled = true; }
    }

    // ------------------------------------------------------------ Janelas do Windows

    private Services.WindowsUiSettings CollectWindows() => new()
    {
        AllowedProcesses = NXPilot.WindowsUi.WindowsUiPolicy.Parse(AllowedAppsBox.Text).Allowed.ToList(),
    };

    /// <summary>Lista TODAS as janelas (só aqui, na tela, para escolher o que liberar); o agente só vê as liberadas.</summary>
    private void OnListWindowsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var ui = new NXPilot.WindowsUi.WindowsUiService(CollectWindows().Policy);
            WindowsList.Items.Clear();
            foreach (var w in ui.ListWindows(allowedOnly: false).OrderByDescending(w => w.Allowed).ThenBy(w => w.ProcessName))
                WindowsList.Items.Add(new ListBoxItem
                {
                    Content = $"{(w.Allowed ? "✓" : "  ")} {w.ProcessName} — {w.Title}",
                    Tag = w,
                    Foreground = w.Allowed ? Brushes.Black : Brushes.Gray,
                    ToolTip = w.Allowed ? null : T($"Não liberado: adicione \"{w.ProcessName}\" à lista para o NXPilot poder ver.", $"Not allowed: add \"{w.ProcessName}\" to the list so NXPilot can see it."),
                });
            Status(WindowsStatus, T($"{WindowsList.Items.Count} janela(s). ✓ = liberada.", $"{WindowsList.Items.Count} window(s). ✓ = allowed."), Brushes.DimGray);
        }
        catch (Exception ex) { Status(WindowsStatus, ex.Message, Brushes.Firebrick); }
    }

    private NXPilot.WindowsUi.WindowInfo? SelectedWindow => (WindowsList.SelectedItem as ListBoxItem)?.Tag as NXPilot.WindowsUi.WindowInfo;

    private void OnWindowSelected(object sender, SelectionChangedEventArgs e) =>
        WindowCaptureButton.IsEnabled = WindowReadButton.IsEnabled = SelectedWindow is { Allowed: true };

    private void OnWindowCaptureClick(object sender, RoutedEventArgs e) => WithWindow((ui, w) =>
    {
        var png = ui.Capture(w, System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nxpilot-janela-{DateTime.Now:yyyyMMdd-HHmmss}.png"));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(png) { UseShellExecute = true });
        return T($"Capturado: {png}", $"Captured: {png}");
    });

    private void OnWindowReadClick(object sender, RoutedEventArgs e) => WithWindow((ui, w) =>
    {
        var elements = ui.ReadElements(w);
        return T($"{elements.Count} elemento(s) — é isto que o agente receberia:\n\n", $"{elements.Count} element(s) — this is what the agent would get:\n\n") + ui.ToAgentContext(w, elements);
    });

    private void WithWindow(Func<NXPilot.WindowsUi.WindowsUiService, NXPilot.WindowsUi.WindowInfo, string> action)
    {
        if (SelectedWindow is not { } w) return;
        try
        {
            using var ui = new NXPilot.WindowsUi.WindowsUiService(CollectWindows().Policy); // confere a lista como está na tela
            Status(WindowsStatus, action(ui, w), Brushes.Black);
        }
        catch (Exception ex) { Status(WindowsStatus, ex.Message, Brushes.Firebrick); }
    }

    // ------------------------------------------------------------ salvar

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CollectDatabricks().Save();
            CollectGit().Save();
            CollectMail().Save();
            CollectWindows().Save();
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
