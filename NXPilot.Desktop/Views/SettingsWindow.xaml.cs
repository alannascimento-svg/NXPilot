// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NXPilot.Updates;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Configurar → Versão: verificar atualização, voltar para uma versão anterior e avisar quando
/// a base (Setup) mudou. Atualizar = baixar o NXPilot-Release.zip, conferir o SHA-256, trocar os
/// arquivos (renomeando os em uso, sem script) e reiniciar o app.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly UpdateService _updates = new();
    private static string AppDir => AppContext.BaseDirectory;

    public SettingsWindow()
    {
        InitializeComponent();
        var v = UpdateService.CurrentVersion;
        CurrentVersionText.Text = T($"Instalada: NXPilot {v.ToString(3)}.{v.Revision:00}", $"Installed: NXPilot {v.ToString(3)}.{v.Revision:00}");
        Loaded += async (_, _) => await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            var versions = await _updates.ListVersionsAsync();
            VersionsBox.Items.Clear();
            foreach (var p in versions)
                VersionsBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.Tag}  ·  {p.PublishedAt.LocalDateTime:dd/MM/yyyy}{(p.Version == UpdateService.CurrentVersion ? T("  (instalada)", "  (installed)") : "")}",
                    Tag = p,
                });
            if (versions.Count == 0) Status(T("Nenhuma versão com pacote de atualização publicada ainda.", "No version with an update package published yet."), Brushes.DimGray);

            var setup = await _updates.GetSetupReleaseAsync();
            SetupBanner.Visibility = setup != null && UpdateService.SetupChanged(UpdateService.InstalledSetupStamp(AppDir), setup.BuiltAt)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { Status(ex.Message, Brushes.Firebrick); }
    }

    private void OnVersionSelected(object sender, SelectionChangedEventArgs e) =>
        InstallVersionButton.IsEnabled = (VersionsBox.SelectedItem as ComboBoxItem)?.Tag is PublishedVersion p && p.Version != UpdateService.CurrentVersion;

    private async void OnCheckClick(object sender, RoutedEventArgs e)
    {
        await Busy(async () =>
        {
            var latest = await _updates.CheckForUpdateAsync();
            if (latest == null) { Status(T("Você já está na versão mais nova.", "You are already on the latest version."), Brushes.Green); return; }
            await InstallAsync(latest, rollback: false);
        });
    }

    private async void OnInstallVersionClick(object sender, RoutedEventArgs e)
    {
        if ((VersionsBox.SelectedItem as ComboBoxItem)?.Tag is not PublishedVersion p) return;
        await Busy(() => InstallAsync(p, rollback: p.Version < UpdateService.CurrentVersion));
    }

    /// <summary>Confirma, baixa, confere, troca os arquivos e reinicia. Tarefas em andamento são interrompidas.</summary>
    private async Task InstallAsync(PublishedVersion version, bool rollback)
    {
        var question = rollback
            ? T($"Voltar para a {version.Tag}? O NXPilot vai fechar e abrir de novo; tarefas em andamento serão interrompidas.",
                $"Go back to {version.Tag}? NXPilot will close and reopen; running tasks will be interrupted.")
            : T($"Atualizar para a {version.Tag}? O NXPilot vai fechar e abrir de novo; tarefas em andamento serão interrompidas.",
                $"Update to {version.Tag}? NXPilot will close and reopen; running tasks will be interrupted.");
        if (MessageBox.Show(this, question, "NXPilot", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        var package = await _updates.DownloadAsync(version, new Progress<string>(m => Status(m, Brushes.DimGray)));
        Status(T("Instalando…", "Installing…"), Brushes.DimGray);
        var count = await Task.Run(() => UpdateService.Apply(package, AppDir));
        try { Directory.Delete(Path.GetDirectoryName(package)!, true); } catch { /* temporário */ }
        Status(T($"{count} arquivo(s) atualizados. Reiniciando…", $"{count} file(s) updated. Restarting…"), Brushes.Green);

        Process.Start(new ProcessStartInfo(Path.Combine(AppDir, "NxPilot.exe")) { WorkingDirectory = AppDir, UseShellExecute = true });
        Application.Current.Shutdown();
    }

    /// <summary>A base mudou (runtime/bibliotecas): baixa o Setup, extrai e abre — ele instala e atualiza o app.</summary>
    private async void OnSetupClick(object sender, RoutedEventArgs e)
    {
        await Busy(async () =>
        {
            var setup = await _updates.GetSetupReleaseAsync() ?? throw new UpdateException(T("Setup não encontrado no GitHub.", "Setup not found on GitHub."));
            var zip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", UpdateService.SetupAsset);
            Status(T("Baixando o Setup…", "Downloading the Setup…"), Brushes.DimGray);
            await _updates.DownloadFileAsync(setup.DownloadUrl, zip);
            var dir = Path.Combine(Path.GetTempPath(), "nxpilot-setup-" + Guid.NewGuid().ToString("N"));
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, dir);
            var exe = Directory.GetFiles(dir, "NXPilot-Setup.exe", SearchOption.AllDirectories).FirstOrDefault()
                      ?? throw new UpdateException(T("O zip do Setup não tem o NXPilot-Setup.exe.", "The Setup zip has no NXPilot-Setup.exe."));
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
            Application.Current.Shutdown(); // o Setup encerra o app se ainda estiver aberto
        });
    }

    private async Task Busy(Func<Task> action)
    {
        CheckButton.IsEnabled = InstallVersionButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        try { await action(); }
        catch (Exception ex) { Status(ex.Message, Brushes.Firebrick); }
        finally
        {
            Progress.Visibility = Visibility.Collapsed;
            CheckButton.IsEnabled = true;
            OnVersionSelected(this, null!);
        }
    }

    private void Status(string text, Brush color) { StatusText.Text = text; StatusText.Foreground = color; }
}
