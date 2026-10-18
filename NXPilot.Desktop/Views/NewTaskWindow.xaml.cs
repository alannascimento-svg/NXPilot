// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NXPilot.Agents;
using NXPilot.Desktop.Services;
using NXPilot.Desktop.ViewModels;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Assistente de Nova tarefa: só coleta e valida; quem roda os agentes é o MainViewModel.
/// Lembra repositório, modelo e revisor da última tarefa (settings.json).
/// </summary>
public partial class NewTaskWindow : Window
{
    public NewTaskInfo? Result { get; private set; }

    /// <summary>Pré-preenchida a partir de um work item do Azure DevOps (título, descrição, critérios).</summary>
    public NewTaskWindow(NXPilot.Integrations.WorkItem item) : this()
    {
        TitleBox.Text = $"#{item.Id} · {item.Title}";
        IntentBox.Text = item.Description.Length > 0 ? item.Description : item.Title;
        CriteriaBox.Text = item.AcceptanceCriteria;
    }

    public NewTaskWindow()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        var s = AppSettings.Load();
        RepoBox.Text = s.LastRepo ?? "";
        CodexModelBox.Text = s.CodexModel ?? "";
        Select(ModelBox, s.LastModel ?? "");
        Select(ReviewerBox, s.Reviewer ?? nameof(ReviewerKind.Codex));

        // Quem tem só o Codex (ou só o Claude) também cria tarefa: mostra o que está instalado e
        // já seleciona o último usado ou, na primeira vez, o que existe na máquina.
        _claudeInstalled = ReviewerChoice.IsInstalled(AgentKind.Claude);
        _codexInstalled = ReviewerChoice.IsInstalled(AgentKind.Codex);
        var notInstalled = T(" — não instalado", " — not installed");
        AgentClaudeItem.Content = "Claude Code" + (_claudeInstalled ? "" : notInstalled);
        AgentCodexItem.Content = "Codex" + (_codexInstalled ? "" : notInstalled);
        Select(AgentBox, s.Executor ?? (!_claudeInstalled && _codexInstalled ? nameof(ExecutorKind.Codex) : nameof(ExecutorKind.Claude)));
        UpdateAgentHint();

        Loaded += async (_, _) =>
        {
            (RepoBox.Text.Length == 0 ? RepoBox : TitleBox).Focus();
            await LoadCodexModelsAsync();
        };
    }

    /// <summary>
    /// Lista do "Modelo do Codex" vinda da PRÓPRIA conta (model/list do Codex) — evita o erro
    /// de modelo não aceito pela conta ChatGPT. Marca os modelos já testados com o NXPilot.
    /// </summary>
    private async Task LoadCodexModelsAsync()
    {
        var tested = string.Join(", ", NXPilot.Agents.CodexAppServerAdapter.TestedModels);
        CodexModelHint.Text = T("Consultando os modelos da sua conta no Codex…", "Querying your account's Codex models…");
        var models = await NXPilot.Agents.CodexAppServerAdapter.ListModelsAsync();
        var typed = CodexModelBox.Text;
        CodexModelBox.ItemsSource = models.Select(m => m.Id).ToList();
        if (typed.Length == 0)
            CodexModelBox.Text = models.FirstOrDefault(m => m.Tested)?.Id ?? models.FirstOrDefault(m => m.IsDefault)?.Id ?? "";
        CodexModelHint.Text = models.Count == 0
            ? T($"Não deu para listar (Codex instalado e logado?). Testado com o NXPilot: {tested}.", $"Could not list models (is Codex installed and signed in?). Tested with NXPilot: {tested}.")
            : T($"{models.Count} modelo(s) da sua conta · testado com o NXPilot: {string.Join(", ", models.Where(m => m.Tested).Select(m => m.Id + " ✓").DefaultIfEmpty(tested + " (não está na sua lista)"))}",
                $"{models.Count} model(s) on your account · tested with NXPilot: {string.Join(", ", models.Where(m => m.Tested).Select(m => m.Id + " ✓").DefaultIfEmpty(tested + " (not on your list)"))}");
    }

    private readonly bool _claudeInstalled, _codexInstalled;

    private ExecutorKind SelectedExecutor =>
        Enum.Parse<ExecutorKind>((AgentBox.SelectedItem as ComboBoxItem)?.Tag as string ?? nameof(ExecutorKind.Claude));

    private void OnAgentChanged(object sender, SelectionChangedEventArgs e) => UpdateAgentHint();

    /// <summary>Explica, embaixo do Agente, que o gate vale igual e quem vai revisar.</summary>
    private void UpdateAgentHint()
    {
        if (AgentHint == null) return; // SelectionChanged dispara durante o InitializeComponent
        var codex = SelectedExecutor == ExecutorKind.Codex;
        var name = codex ? "Codex" : "Claude Code";
        var hint = T($"O {name} executa sob o mesmo gate: cada comando e arquivo passa pela cascata e pelas suas decisões.",
                     $"{name} runs under the same gate: every command and file goes through the cascade and your decisions.");
        if (_claudeInstalled != _codexInstalled)
            hint += " " + T($"Só o {(_codexInstalled ? "Codex" : "Claude Code")} está instalado: a revisão será dele mesmo, numa sessão nova e somente leitura.",
                            $"Only {(_codexInstalled ? "Codex" : "Claude Code")} is installed: it reviews itself, in a new read-only session.");
        AgentHint.Text = hint;
    }

    private static void Select(ComboBox box, string tag)
    {
        foreach (ComboBoxItem item in box.Items)
            if ((item.Tag as string) == tag) { box.SelectedItem = item; return; }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = T("Repositório git da tarefa", "Git repository for the task") };
        if (Directory.Exists(RepoBox.Text)) dialog.InitialDirectory = RepoBox.Text;
        if (dialog.ShowDialog(this) == true) RepoBox.Text = dialog.FolderName;
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var repo = RepoBox.Text.Trim();
        var reviewer = Enum.Parse<ReviewerKind>((ReviewerBox.SelectedItem as ComboBoxItem)?.Tag as string ?? nameof(ReviewerKind.Codex));
        var executor = SelectedExecutor;
        string? error = null;
        if (executor == ExecutorKind.Codex ? !_codexInstalled : !_claudeInstalled)
            error = executor == ExecutorKind.Codex
                ? T("O Codex não foi encontrado nesta máquina (PATH, NXProject ou extensão do VS Code). Instale e faça login, ou escolha o Claude Code.",
                    "Codex was not found on this machine (PATH, NXProject or VS Code extension). Install it and sign in, or choose Claude Code.")
                : T("O Claude Code não foi encontrado nesta máquina. Instale e faça login, ou escolha o Codex.",
                    "Claude Code was not found on this machine. Install it and sign in, or choose Codex.");
        else if (!Directory.Exists(repo)) error = T("Escolha a pasta de um repositório git.", "Choose a git repository folder.");
        else if (!Directory.Exists(Path.Combine(repo, ".git")) && !File.Exists(Path.Combine(repo, ".git")))
            error = T("A pasta não tem .git — escolha a raiz de um repositório git.", "The folder has no .git — choose the root of a git repository.");
        else if (TitleBox.Text.Trim().Length == 0) error = T("Dê um título à tarefa.", "Give the task a title.");
        else if (IntentBox.Text.Trim().Length < 10) error = T("Descreva a intenção (o que deve ser feito).", "Describe the intent (what should be done).");
        else if (executor == ExecutorKind.Codex && CodexModelBox.Text.Trim().Length == 0)
            error = T("Com o Codex executando, informe o modelo do Codex (ex.: gpt-5.6-terra).", "With Codex executing, set the Codex model (e.g. gpt-5.6-terra).");

        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var model = (ModelBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var codexModel = CodexModelBox.Text.Trim();
        AppSettings.Update(s =>
        {
            s.LastRepo = repo;
            s.LastModel = model;
            s.CodexModel = codexModel;
            s.Reviewer = reviewer.ToString();
            s.Executor = executor.ToString();
        });

        Result = new NewTaskInfo(repo, TitleBox.Text.Trim(), IntentBox.Text.Trim(),
            CriteriaBox.Text.Trim().Length > 0 ? CriteriaBox.Text.Trim() : T("(não informados)", "(not provided)"),
            model.Length > 0 ? model : null, reviewer, codexModel.Length > 0 ? codexModel : null, executor);
        DialogResult = true;
    }
}
