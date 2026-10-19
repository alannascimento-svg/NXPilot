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
/// Processos de clicar/digitar. A pessoa (ou o agente) monta a sequência com elementos reais do
/// app; a pessoa APROVA o processo inteiro (hash do conteúdo — editar derruba a aprovação).
/// Ao executar: alvo ausente/ambíguo para tudo; clique de EXCLUSÃO pede nova autorização na hora.
/// </summary>
public partial class ProcessesWindow : Window
{
    private readonly List<UiProcess> _processes = Services.UiProcessStore.Load();
    private List<UiStep> _steps = new();
    private UiProcess? _current;
    private List<UiElementInfo> _elements = new();
    private CancellationTokenSource? _run;

    public ProcessesWindow()
    {
        InitializeComponent();
        foreach (var k in UiStep.AllowedKeys) KeyBox.Items.Add(k);
        KeyBox.SelectedIndex = 0;
        Loaded += (_, _) => { LoadApps(); RefreshList(); NewProcess(); };
        Closing += (_, _) => _run?.Cancel();
    }

    // ------------------------------------------------------------ lista e edição

    private void RefreshList()
    {
        ProcessList.Items.Clear();
        foreach (var p in _processes)
            ProcessList.Items.Add(new ListBoxItem { Content = $"{(p.IsApproved ? "✓" : "•")} {p.Name}  ({p.ProcessName})", Tag = p });
    }

    private void LoadApps()
    {
        AppsBox.Items.Clear();
        using var ui = new WindowsUiService(new WindowsUiPolicy([]));
        foreach (var w in ui.ListWindows(allowedOnly: false).OrderBy(w => w.ProcessName))
            AppsBox.Items.Add(new ComboBoxItem { Content = $"{w.ProcessName} — {w.Title}{(w.Minimized ? T("  (minimizado)", "  (minimized)", "  (minimizada)") : "")}", Tag = w });
    }

    private WindowInfo? SelectedApp => (AppsBox.SelectedItem as ComboBoxItem)?.Tag as WindowInfo;

    private string AppName => SelectedApp?.ProcessName ?? _current?.ProcessName ?? "";

    private void NewProcess()
    {
        _current = null;
        _steps = new List<UiStep>();
        NameBox.Text = T("Novo processo", "New process", "Nuevo proceso");
        RefreshSteps();
    }

    private void OnNewClick(object sender, RoutedEventArgs e) => NewProcess();

    private void OnProcessSelected(object sender, SelectionChangedEventArgs e)
    {
        if ((ProcessList.SelectedItem as ListBoxItem)?.Tag is not UiProcess p) return;
        _current = p;
        _steps = p.Steps.ToList();
        NameBox.Text = p.Name;
        foreach (ComboBoxItem item in AppsBox.Items)
            if (item.Tag is WindowInfo w && string.Equals(w.ProcessName, p.ProcessName, StringComparison.OrdinalIgnoreCase)) { AppsBox.SelectedItem = item; break; }
        RefreshSteps();
    }

    private void OnRemoveProcessClick(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (MessageBox.Show(this, T($"Remover o processo \"{_current.Name}\" do NXPilot? (Não mexe no app.)", $"Remove process \"{_current.Name}\" from NXPilot? (Does not touch the app.)", $"¿Quitar el proceso \"{_current.Name}\" de NXPilot? (No afecta a la app.)"),
                "NXPilot", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _processes.Remove(_current);
        Services.UiProcessStore.Save(_processes);
        RefreshList();
        NewProcess();
    }

    /// <summary>O processo como está na tela (com a aprovação anterior — que só vale se nada mudou).</summary>
    private UiProcess Draft() => new(NameBox.Text.Trim(), AppName, _steps.ToList())
    {
        Author = _current?.Author ?? T("usuário", "user", "usuario"),
        ApprovedHash = _current?.ApprovedHash,
        ApprovedAt = _current?.ApprovedAt,
    };

    private void RefreshSteps()
    {
        StepsList.Items.Clear();
        for (var i = 0; i < _steps.Count; i++)
        {
            var s = _steps[i];
            StepsList.Items.Add(new ListBoxItem
            {
                Content = $"{i + 1}. {(s.IsDestructive ? "⚠ " : "")}{s.Describe()}",
                Foreground = s.IsDestructive ? Brushes.Firebrick : Brushes.Black,
                ToolTip = s.IsDestructive ? T("Exclusão: vai pedir nova autorização na hora de executar.", "Deletion: will ask again for authorization when it runs.", "Eliminación: pedirá autorización de nuevo al ejecutarse.") : null,
            });
        }
        var d = Draft();
        ApprovalText.Text = d.IsApproved
            ? T($"✓ Aprovado em {d.ApprovedAt:dd/MM HH:mm} · escrito por: {d.Author}", $"✓ Approved on {d.ApprovedAt:MM/dd HH:mm} · written by: {d.Author}", $"✓ Aprobado el {d.ApprovedAt:dd/MM HH:mm} · escrito por: {d.Author}")
            : T($"Não aprovado · escrito por: {d.Author}", $"Not approved · written by: {d.Author}", $"No aprobado · escrito por: {d.Author}");
        ApprovalText.Foreground = d.IsApproved ? Brushes.Green : Brushes.DarkOrange;
    }

    // ------------------------------------------------------------ elementos e passos

    private void OnLoadElementsClick(object sender, RoutedEventArgs e)
    {
        if (SelectedApp is not { } w) return;
        try
        {
            using var ui = new WindowsUiService(new WindowsUiPolicy([w.ProcessName])); // escolhido pela pessoa
            _elements = ui.ReadElements(w, maxDepth: 12, maxItems: 1500)
                .Where(x => x.Name.Length > 0 || x.AutomationId.Length > 0).ToList();
            OnFilterChanged(this, null!);
            Log(T($"{_elements.Count} elemento(s) de {w.ProcessName}.", $"{_elements.Count} element(s) from {w.ProcessName}.", $"{_elements.Count} elemento(s) de {w.ProcessName}."));
        }
        catch (Exception ex) { Log(ex.Message); }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        ElementsList.Items.Clear();
        foreach (var x in _elements.Where(x => FilterBox.Text.Length == 0 || MonitorMatcher.Contains($"{x.ControlType} {x.Name} {x.AutomationId}", FilterBox.Text)).Take(500))
            ElementsList.Items.Add(new ListBoxItem
            {
                Content = $"[{x.ControlType}] {(x.Name.Length > 0 ? x.Name : "")}{(x.AutomationId.Length > 0 ? $"  #{x.AutomationId}" : "")}",
                Tag = x,
                Foreground = UiStep.LooksDestructive(x.Name) || UiStep.LooksDestructive(x.AutomationId) ? Brushes.Firebrick : Brushes.Black,
            });
    }

    private UiTarget? SelectedTarget => (ElementsList.SelectedItem as ListBoxItem)?.Tag is UiElementInfo x
        ? new UiTarget(x.ControlType, x.AutomationId.Length > 0 ? x.AutomationId : null, x.Name.Length > 0 ? x.Name : null)
        : null;

    private void AddStep(UiStep? step)
    {
        if (step == null) { Log(T("Escolha um elemento na lista do meio.", "Pick an element in the middle list.", "Elija un elemento en la lista del medio.")); return; }
        _steps.Add(step);
        RefreshSteps();
    }

    private void OnAddClickClick(object sender, RoutedEventArgs e) =>
        AddStep(SelectedTarget is { } t ? new UiStep(UiStepKind.Click, t) : null);

    private void OnAddTypeClick(object sender, RoutedEventArgs e) =>
        AddStep(SelectedTarget is { } t ? new UiStep(UiStepKind.Type, t, Text: TypeTextBox.Text) : null);

    private void OnAddKeyClick(object sender, RoutedEventArgs e) => AddStep(new UiStep(UiStepKind.Key, Key: KeyBox.SelectedItem as string));

    private void OnAddWaitClick(object sender, RoutedEventArgs e) =>
        AddStep(new UiStep(UiStepKind.Wait, WaitSeconds: int.TryParse(WaitBox.Text, out var s) ? s : 0));

    private void Move(int delta)
    {
        var i = StepsList.SelectedIndex;
        if (i < 0 || i + delta < 0 || i + delta >= _steps.Count) return;
        (_steps[i], _steps[i + delta]) = (_steps[i + delta], _steps[i]);
        RefreshSteps();
        StepsList.SelectedIndex = i + delta;
    }

    private void OnUpClick(object sender, RoutedEventArgs e) => Move(-1);
    private void OnDownClick(object sender, RoutedEventArgs e) => Move(1);

    private void OnRemoveStepClick(object sender, RoutedEventArgs e)
    {
        if (StepsList.SelectedIndex < 0) return;
        _steps.RemoveAt(StepsList.SelectedIndex);
        RefreshSteps();
    }

    // ------------------------------------------------------------ salvar, aprovar, executar

    private UiProcess SaveDraft(UiProcess p)
    {
        if (_current != null) _processes[_processes.IndexOf(_current)] = p; else _processes.Add(p);
        _current = p;
        Services.UiProcessStore.Save(_processes);
        RefreshList();
        RefreshSteps();
        return p;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var p = SaveDraft(Draft());
        Log(p.IsApproved ? T("Salvo (continua aprovado).", "Saved (still approved).", "Guardado (sigue aprobado).") : T("Salvo. Revise e aprove para poder executar.", "Saved. Review and approve it to run.", "Guardado. Revíselo y apruébelo para ejecutarlo."));
    }

    private void OnApproveClick(object sender, RoutedEventArgs e)
    {
        var d = Draft();
        var problems = d.Validate();
        if (d.ProcessName.Length == 0) problems = problems.Append(T("Escolha o app.", "Pick the app.", "Elija la app.")).ToList();
        if (problems.Count > 0) { Log(string.Join("\n", problems)); return; }

        var summary = string.Join("\n", d.Steps.Select((s, i) => $"{i + 1}. {(s.IsDestructive ? "⚠ " : "")}{s.Describe()}"));
        var warn = d.Steps.Any(s => s.IsDestructive)
            ? T("\n\n⚠ Passos de exclusão vão pedir NOVA autorização na hora de executar.", "\n\n⚠ Deletion steps will ask for authorization AGAIN when they run.", "\n\n⚠ Los pasos de eliminación pedirán autorización OTRA VEZ al ejecutarse.") : "";
        var msg = T($"Aprovar o processo \"{d.Name}\" no app {d.ProcessName}?\n\n{summary}{warn}\n\nQualquer edição depois desfaz a aprovação.",
                    $"Approve process \"{d.Name}\" in app {d.ProcessName}?\n\n{summary}{warn}\n\nAny later edit undoes the approval.",
                    $"¿Aprobar el proceso \"{d.Name}\" en la app {d.ProcessName}?\n\n{summary}{warn}\n\nCualquier edición posterior anula la aprobación.");
        if (MessageBox.Show(this, msg, "NXPilot", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        SaveDraft(d.Approve());
        Log(T("Processo aprovado.", "Process approved.", "Proceso aprobado."));
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        var p = Draft();
        if (!p.IsApproved) { Log(T("Aprove o processo antes de executar (ou ele foi alterado depois da aprovação).", "Approve the process before running it (or it changed after approval).", "Apruebe el proceso antes de ejecutarlo (o cambió después de la aprobación).")); return; }
        _run = new CancellationTokenSource();
        RunButton.IsEnabled = false; StopButton.IsEnabled = true;
        RunLog.Clear();
        try
        {
            var results = await RunAsync(this, p, line => Log(line), _run.Token);
            Log(results.All(r => r.Ok) ? T("✓ Processo concluído.", "✓ Process finished.", "✓ Proceso concluido.") : T("Processo interrompido.", "Process interrupted.", "Proceso interrumpido."));
        }
        catch (Exception ex) { Log(ex.Message); }
        finally { _run.Dispose(); _run = null; RunButton.IsEnabled = true; StopButton.IsEnabled = false; }
    }

    private void OnStopClick(object sender, RoutedEventArgs e) => _run?.Cancel();

    /// <summary>
    /// Executa um processo aprovado no app dele (também usado pelo monitor ao disparar). Exclusão
    /// pede nova autorização numa janela — sem resposta "Sim", nada é excluído.
    /// </summary>
    public static async Task<IReadOnlyList<UiStepResult>> RunAsync(Window owner, UiProcess process, Action<string> log, CancellationToken ct)
    {
        using var ui = new WindowsUiService(new WindowsUiPolicy([process.ProcessName]));
        var window = ui.FindWindow(process.ProcessName)
                     ?? throw new InvalidOperationException(T($"O app {process.ProcessName} não está aberto.", $"App {process.ProcessName} is not open.", $"La app {process.ProcessName} no está abierta."));
        var driver = ui.Driver(window);
        return await UiProcessRunner.RunAsync(process, driver,
            new Progress<UiStepResult>(r => log($"{(r.Ok ? "✓" : "✗")} {r.Index + 1}. {r.Message}")), ct,
            (step, live, _) => Task.FromResult(owner.Dispatcher.Invoke(() => MessageBox.Show(owner,
                T($"O passo vai EXCLUIR:\n\n  {step.Describe()}\n  (na tela: \"{live}\")\n\nAutorizar esta exclusão?", $"This step will DELETE:\n\n  {step.Describe()}\n  (on screen: \"{live}\")\n\nAuthorize this deletion?", $"Este paso va a ELIMINAR:\n\n  {step.Describe()}\n  (en pantalla: \"{live}\")\n\n¿Autoriza esta eliminación?"),
                T("NXPilot — confirmar exclusão", "NXPilot — confirm deletion", "NXPilot — confirmar eliminación"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)));
    }

    private void Log(string text) => RunLog.AppendText((RunLog.Text.Length > 0 ? Environment.NewLine : "") + text);
}
