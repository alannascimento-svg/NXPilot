// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NXPilot.WindowsUi;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Ler / monitorar um app escolhido pela pessoa (inclusive minimizado). A escolha vale como
/// autorização daquela leitura: o serviço é criado com uma política só desse processo.
/// Só lê — nunca clica, fecha ou digita.
/// </summary>
public partial class ReadAppWindow : Window
{
    private CancellationTokenSource? _monitor;

    public ReadAppWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { LoadApps(); LoadActions(); };
        Closing += (_, _) => _monitor?.Cancel(); // fechar a janela encerra o monitor
    }

    private void LoadApps()
    {
        AppsBox.Items.Clear();
        using var ui = new WindowsUiService(new WindowsUiPolicy([]));
        foreach (var w in ui.ListWindows(allowedOnly: false).OrderBy(w => w.ProcessName))
            AppsBox.Items.Add(new ComboBoxItem
            {
                Content = $"{w.ProcessName} — {w.Title}{(w.Minimized ? T("  (minimizado)", "  (minimized)") : "")}",
                Tag = w,
            });
        if (AppsBox.Items.Count > 0) AppsBox.SelectedIndex = 0;
    }

    /// <summary>Só processos APROVADOS podem ser a ação do monitor.</summary>
    private void LoadActions()
    {
        ActionBox.Items.Clear();
        ActionBox.Items.Add(new ComboBoxItem { Content = T("Só avisar (nenhuma ação)", "Only alert (no action)") });
        foreach (var p in Services.UiProcessStore.Load().Where(p => p.IsApproved))
            ActionBox.Items.Add(new ComboBoxItem { Content = T($"Executar o processo \"{p.Name}\" ({p.ProcessName})", $"Run process \"{p.Name}\" ({p.ProcessName})"), Tag = p });
        ActionBox.SelectedIndex = 0;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) { LoadApps(); LoadActions(); }

    private WindowInfo? Selected => (AppsBox.SelectedItem as ComboBoxItem)?.Tag as WindowInfo;

    /// <summary>Serviço autorizado SÓ para o app escolhido (a escolha é a autorização).</summary>
    private static WindowsUiService For(WindowInfo w) => new(new WindowsUiPolicy([w.ProcessName]));

    private async void OnReadClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } w) return;
        ReadButton.IsEnabled = false;
        Status(T("Lendo…", "Reading…"), Brushes.DimGray);
        try
        {
            var text = await Task.Run(() => { using var ui = For(w); return ui.ReadText(w); });
            TextBox.Text = text;
            Status(text.Length == 0
                ? T("O app não expôs texto pela acessibilidade (comum em imagens, PDF escaneado e áreas remotas).", "The app exposed no text through accessibility (common for images, scanned PDFs and remote desktops).")
                : T($"{text.Length} caracteres lidos de {w.ProcessName}. Revise antes de usar.", $"{text.Length} characters read from {w.ProcessName}. Review before using."),
                text.Length == 0 ? Brushes.DarkOrange : Brushes.Green);
        }
        catch (Exception ex) { Status(ex.Message, Brushes.Firebrick); }
        finally { ReadButton.IsEnabled = true; }
    }

    private async void OnMonitorClick(object sender, RoutedEventArgs e)
    {
        if (_monitor != null) { _monitor.Cancel(); return; } // Parar
        if (Selected is not { } w) return;

        MonitorOptions options;
        try
        {
            options = new MonitorOptions(TimeSpan.FromSeconds(int.TryParse(IntervalBox.Text, out var s) ? s : 0),
                                         TimeSpan.FromMinutes(int.TryParse(DurationBox.Text, out var m) ? m : 0))
                      { Keywords = MonitorOptions.ParseKeywords(KeywordsBox.Text), Origin = OriginBox.Text.Trim() is { Length: > 0 } o ? o : null };
        }
        catch (ArgumentOutOfRangeException ex) { Status(ex.Message.Split(" (")[0], Brushes.Firebrick); return; }

        var action = (ActionBox.SelectedItem as ComboBoxItem)?.Tag as UiProcess;
        var where = options.Origin != null ? T($" na área \"{options.Origin}\"", $" in the \"{options.Origin}\" area") : "";
        var what = options.Keywords.Count > 0
            ? T($"avisando quando aparecer{where}: {string.Join(", ", options.Keywords)}", $"alerting when it shows{where}: {string.Join(", ", options.Keywords)}")
            : T($"avisando a cada linha nova{where}", $"alerting on every new line{where}");
        var then = action != null
            ? T($"\n\nAo disparar, executa o processo aprovado \"{action.Name}\" (no máximo 1 vez por leitura; exclusões pedem confirmação).",
                $"\n\nWhen it fires, runs the approved process \"{action.Name}\" (at most once per read; deletions ask for confirmation).")
            : "";
        var confirm = T($"Monitorar {w.ProcessName} a cada {options.Interval.TotalSeconds:F0} s por {options.Duration.TotalMinutes:F0} min, {what}? Só leitura; para sozinho no fim do tempo.{then}",
                        $"Monitor {w.ProcessName} every {options.Interval.TotalSeconds:F0} s for {options.Duration.TotalMinutes:F0} min, {what}? Read-only; stops by itself when time is up.{then}");
        if (MessageBox.Show(this, confirm, "NXPilot", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        _monitor = new CancellationTokenSource();
        MonitorButton.Content = T("Parar", "Stop");
        MonitorLog.Items.Clear();
        var until = DateTime.Now + options.Duration;
        Status(T($"Monitorando até {until:HH:mm}…", $"Monitoring until {until:HH:mm}…"), Brushes.DimGray);
        try
        {
            using var ui = For(w);
            await ui.MonitorAsync(w, options, new Progress<WindowsUiService.MonitorReading>(r =>
            {
                if (r.Alert)
                {
                    // Avisa com as linhas que dispararam; a área inteira (origem) fica no texto para usar.
                    foreach (var line in r.Matches)
                        MonitorLog.Items.Insert(0, new ListBoxItem { Content = $"🔔 {r.At:HH:mm:ss}  {line}", FontWeight = FontWeights.SemiBold });
                    TextBox.Text = r.Text;
                    System.Media.SystemSounds.Asterisk.Play();
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Activate();
                    if (action != null && !_acting) _ = ActAsync(action);
                }
                else
                    MonitorLog.Items.Insert(0, new ListBoxItem
                    {
                        Content = $"{r.At:HH:mm:ss}  #{r.Number}  " + (!r.OriginVisible ? T("origem não está na tela — aguardando", "origin is not on screen — waiting")
                            : r.Number == 1 ? T("primeira leitura (estado inicial)", "first read (initial state)") : T("nada novo", "nothing new")),
                        Foreground = Brushes.Gray,
                    });
            }), _monitor.Token);
            Status(T("Monitor encerrado.", "Monitor finished."), Brushes.DimGray);
        }
        catch (Exception ex) { Status(ex.Message, Brushes.Firebrick); }
        finally
        {
            _monitor?.Dispose();
            _monitor = null;
            MonitorButton.Content = T("Iniciar", "Start");
        }
    }

    private bool _acting;

    /// <summary>Ação ao disparar: o processo aprovado, uma execução por vez (nunca em paralelo nem em loop).</summary>
    private async Task ActAsync(UiProcess process)
    {
        _acting = true;
        try
        {
            MonitorLog.Items.Insert(0, T($"▶ Executando o processo \"{process.Name}\"…", $"▶ Running process \"{process.Name}\"…"));
            var results = await ProcessesWindow.RunAsync(this, process, line => MonitorLog.Items.Insert(0, "   " + line), _monitor?.Token ?? CancellationToken.None);
            MonitorLog.Items.Insert(0, results.All(x => x.Ok) ? T("✓ Processo concluído.", "✓ Process finished.") : T("✗ Processo interrompido.", "✗ Process interrupted."));
        }
        catch (Exception ex) { MonitorLog.Items.Insert(0, "✗ " + ex.Message); }
        finally { _acting = false; }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(TextBox.Text); Status(T("Copiado.", "Copied."), Brushes.Green); }
        catch { /* área de transferência ocupada */ }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void Status(string text, Brush color) { StatusText.Text = text; StatusText.Foreground = color; }
}
