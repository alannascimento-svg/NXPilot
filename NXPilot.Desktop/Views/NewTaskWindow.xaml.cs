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
        RefreshCodexModelsButton.Content = T("↻ Modelos", "↻ Models", "↻ Modelos");
        UpdateCodexButton.Content = T("Atualizar Codex", "Update Codex", "Actualizar Codex");
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        var s = AppSettings.Load();
        RepoBox.Text = s.LastRepo ?? "";
        CodexModelBox.Text = s.CodexModel ?? "";
        Select(ModelBox, s.LastModel ?? "");
        Select(ReviewerBox, s.Reviewer ?? nameof(ReviewerKind.Codex));
        Select(VersioningBox, s.Versioning ?? nameof(VersioningMode.Local));
        UpdateVersioningHint();
        LoadSubjects(s.LastSubject);

        // Quem tem só o Codex (ou só o Claude) também cria tarefa: mostra o que está instalado e
        // já seleciona o último usado ou, na primeira vez, o que existe na máquina.
        _claudeInstalled = ReviewerChoice.IsInstalled(AgentKind.Claude);
        _codexInstalled = ReviewerChoice.IsInstalled(AgentKind.Codex);
        var notInstalled = T(" — não instalado", " — not installed", " — no instalado");
        AgentClaudeItem.Content = "Claude Code" + (_claudeInstalled ? "" : notInstalled);
        AgentCodexItem.Content = "Codex" + (_codexInstalled ? "" : notInstalled);
        AgentNXAgenticItem.Content = "NXAgentic" + T(" — em construção", " — under construction", " — en construcción");
        Select(AgentBox, s.Executor ?? (!_claudeInstalled && _codexInstalled ? nameof(ExecutorKind.Codex) : nameof(ExecutorKind.Claude)));
        UpdateAgentHint();

        Loaded += async (_, _) =>
        {
            (SelectedVersioning == VersioningMode.Git && RepoBox.Text.Length == 0 ? RepoBox : TitleBox).Focus();
            await LoadCodexModelsAsync();
        };
    }

    /// <summary>
    /// Lista do "Modelo do Codex" vinda da PRÓPRIA conta (model/list do Codex) — evita o erro
    /// de modelo não aceito pela conta ChatGPT. Mostra a versão do cliente e o padrão da conta.
    /// </summary>
    private async Task LoadCodexModelsAsync()
    {
        if (_loadingCodexModels) return;
        _loadingCodexModels = true;
        RefreshCodexModelsButton.IsEnabled = UpdateCodexButton.IsEnabled = false;
        CodexModelHint.Text = T("Consultando os modelos da sua conta no Codex…", "Querying your account's Codex models…", "Consultando los modelos de Codex de su cuenta…");
        try
        {
            var executable = CodexAdapter.Locate();
            var version = executable == null ? null : await Task.Run(() => NXData.AgentInstaller.AgentLocator.Version(executable));
            var models = await CodexAppServerAdapter.ListModelsAsync(executable);
            var selected = CodexAppServerAdapter.SelectModel(models, CodexModelBox.Text);
            CodexModelBox.ItemsSource = models.Select(m => m.Id).ToList();
            CodexModelBox.Text = selected;
            var defaultModel = models.FirstOrDefault(m => m.IsDefault)?.Id ?? "—";
            CodexModelHint.Text = models.Count == 0
                ? T($"Codex {version ?? "—"}: não foi possível listar. Verifique o login ou use Atualizar Codex.", $"Codex {version ?? "—"}: could not list models. Check sign-in or use Update Codex.", $"Codex {version ?? "—"}: no se pudieron listar los modelos. Compruebe el inicio de sesión o use Actualizar Codex.")
                : T($"{models.Count} modelo(s) · Codex {version ?? "—"} · padrão da conta: {defaultModel}",
                    $"{models.Count} model(s) · Codex {version ?? "—"} · account default: {defaultModel}",
                    $"{models.Count} modelo(s) · Codex {version ?? "—"} · predeterminado de la cuenta: {defaultModel}");
            if (models.Count > 0 && !models.Any(m => m.Id.Equals(selected, StringComparison.OrdinalIgnoreCase)))
                CodexModelHint.Text += T(" O modelo informado não está no catálogo atual.", " The entered model is not in the current catalog.", " El modelo indicado no está en el catálogo actual.");
        }
        catch (Exception ex)
        {
            CodexModelHint.Text = T("Falha ao consultar modelos: ", "Could not load models: ", "Error al consultar los modelos: ") + ex.Message;
        }
        finally
        {
            _loadingCodexModels = false;
            RefreshCodexModelsButton.IsEnabled = UpdateCodexButton.IsEnabled = true;
        }
    }

    private bool _loadingCodexModels;

    private async void OnRefreshCodexModelsClick(object sender, RoutedEventArgs e) => await LoadCodexModelsAsync();

    private async void OnUpdateCodexClick(object sender, RoutedEventArgs e)
    {
        RefreshCodexModelsButton.IsEnabled = UpdateCodexButton.IsEnabled = false;
        try
        {
            var installer = new NXData.AgentInstaller.AgentInstaller();
            await installer.InstallAsync(NXData.AgentInstaller.AgentCli.Codex, NXData.AgentInstaller.InstallMode.Internal,
                NXData.AgentInstaller.VersionChoice.Latest, new Progress<string>(message => CodexModelHint.Text = message));
            _codexInstalled = ReviewerChoice.IsInstalled(AgentKind.Codex);
            AgentCodexItem.Content = "Codex";
            UpdateAgentHint();
            await LoadCodexModelsAsync();
        }
        catch (Exception ex) { CodexModelHint.Text = T("Falha ao atualizar Codex: ", "Could not update Codex: ", "Error al actualizar Codex: ") + ex.Message; }
        finally { RefreshCodexModelsButton.IsEnabled = UpdateCodexButton.IsEnabled = true; }
    }

    private readonly bool _claudeInstalled;
    private bool _codexInstalled;

    /// <summary>Edição comercial (repositório privado): já aparece na lista, mas ainda não executa tarefas.</summary>
    private bool NXAgenticSelected => AgentBox.SelectedItem == AgentNXAgenticItem;

    private static string NXAgenticUnderConstruction => T(
        "O NXAgentic (edição comercial da Nexus XData) está em construção e ainda não executa tarefas. Por enquanto, escolha o Claude Code ou o Codex.",
        "NXAgentic (Nexus XData's commercial edition) is under construction and cannot run tasks yet. For now, choose Claude Code or Codex.",
        "NXAgentic (la edición comercial de Nexus XData) está en construcción y aún no ejecuta tareas. Por ahora, elija Claude Code o Codex.");

    private ExecutorKind SelectedExecutor =>
        Enum.TryParse<ExecutorKind>((AgentBox.SelectedItem as ComboBoxItem)?.Tag as string, out var kind) ? kind : ExecutorKind.Claude;

    private void OnAgentChanged(object sender, SelectionChangedEventArgs e) => UpdateAgentHint();

    /// <summary>Explica, embaixo do Agente, que o gate vale igual e quem vai revisar.</summary>
    private void UpdateAgentHint()
    {
        if (AgentHint == null) return; // SelectionChanged dispara durante o InitializeComponent
        if (NXAgenticSelected) { AgentHint.Text = NXAgenticUnderConstruction; return; }
        var codex = SelectedExecutor == ExecutorKind.Codex;
        var name = codex ? "Codex" : "Claude Code";
        var hint = T($"O {name} executa sob o mesmo gate: cada comando e arquivo passa pela cascata e pelas suas decisões.",
                     $"{name} runs under the same gate: every command and file goes through the cascade and your decisions.",
                     $"{name} se ejecuta bajo el mismo gate: cada comando y archivo pasa por la cascada y por sus decisiones.");
        if (ReviewerBox?.SelectedItem is ComboBoxItem { Tag: string reviewer })
            hint += reviewer == nameof(ReviewerKind.None)
                ? T(" Revisão desligada.", " Review is off.", " Revisión desactivada.")
                : T($" Revisor escolhido: {(reviewer == nameof(ReviewerKind.Codex) ? "Codex" : "Claude Code")}, em sessão nova e somente leitura.",
                    $" Selected reviewer: {(reviewer == nameof(ReviewerKind.Codex) ? "Codex" : "Claude Code")}, in a new read-only session.",
                    $" Revisor elegido: {(reviewer == nameof(ReviewerKind.Codex) ? "Codex" : "Claude Code")}, en una sesión nueva de solo lectura.");
        AgentHint.Text = hint;
    }

    private VersioningMode SelectedVersioning =>
        Enum.TryParse<VersioningMode>((VersioningBox.SelectedItem as ComboBoxItem)?.Tag as string, out var mode) ? mode : VersioningMode.Local;

    private ShadowSubject? SelectedSubject => SubjectBox.SelectedItem as ShadowSubject;

    private void OnVersioningChanged(object sender, SelectionChangedEventArgs e) => UpdateVersioningHint();

    /// <summary>Local: um assunto (pastas sem git, histórico no repositório-sombra). Git: o repositório do projeto.</summary>
    private void UpdateVersioningHint()
    {
        if (VersioningHint == null || SubjectRow == null || RepoRow == null) return; // SelectionChanged dispara durante o InitializeComponent
        var local = SelectedVersioning == VersioningMode.Local;
        SubjectRow.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        RepoRow.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        VersioningHint.Text = local
            ? T("Cada assunto é uma pasta comum, sem precisar de git. O NXPilot guarda o histórico à parte (a pasta não ganha .git) e só grava nela quando você aplicar o resultado. Dá para mover o assunto para outra pasta ou para o GitHub depois.",
                "Each subject is a regular folder, no git needed. NXPilot keeps the history separately (no .git is added to the folder) and only writes to it when you apply the result. The subject can be moved to another folder or to GitHub later.",
                "Cada asunto es una carpeta común, sin necesidad de git. NXPilot guarda el historial aparte (la carpeta no recibe .git) y solo escribe en ella cuando usted aplica el resultado. El asunto se puede mover a otra carpeta o a GitHub después.")
            : T("A pasta de um clone do GitHub, Azure DevOps ou outro git. A tarefa vira um branch nxpilot/<id> no próprio repositório; juntar (merge/PR) é decisão sua.",
                "A clone folder from GitHub, Azure DevOps or another git host. The task becomes an nxpilot/<id> branch in the repository itself; merging (merge/PR) is up to you.",
                "La carpeta de un clon de GitHub, Azure DevOps u otro git. La tarea se convierte en una rama nxpilot/<id> en el propio repositorio; integrar (merge/PR) es decisión suya.");
    }

    /// <summary>Recarrega os assuntos (depois de editar na janela Assuntos) mantendo o escolhido.</summary>
    private void LoadSubjects(string? select)
    {
        var subjects = ShadowRepo.LoadSubjects();
        SubjectBox.ItemsSource = subjects;
        SubjectBox.SelectedItem = subjects.FirstOrDefault(s => s.Name == select) ?? subjects.FirstOrDefault();
        UpdateSubjectHint();
    }

    private void OnSubjectChanged(object sender, SelectionChangedEventArgs e) => UpdateSubjectHint();

    private void UpdateSubjectHint()
    {
        if (SubjectHint == null) return;
        SubjectHint.Text = SelectedSubject is { } s
            ? s.Path + (s.WebUrl is { } url ? "  ·  GitHub: " + url : "")
            : T("Nenhum assunto ainda — crie um em \"Assuntos…\" e coloque os arquivos na pasta dele.", "No subjects yet — create one in \"Subjects…\" and put the files in its folder.", "Aún no hay asuntos — cree uno en \"Asuntos…\" y ponga los archivos en su carpeta.");
    }

    private void OnSubjectsClick(object sender, RoutedEventArgs e)
    {
        var window = new SubjectsWindow(SelectedSubject?.Name) { Owner = this };
        window.ShowDialog();
        LoadSubjects(window.SelectedName ?? SelectedSubject?.Name);
    }

    private static void Select(ComboBox box, string tag)
    {
        foreach (ComboBoxItem item in box.Items)
            if ((item.Tag as string) == tag) { box.SelectedItem = item; return; }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = T("Repositório git da tarefa", "Git repository for the task", "Repositorio git de la tarea") };
        if (Directory.Exists(RepoBox.Text)) dialog.InitialDirectory = RepoBox.Text;
        if (dialog.ShowDialog(this) == true) RepoBox.Text = dialog.FolderName;
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var repo = RepoBox.Text.Trim();
        var reviewer = Enum.Parse<ReviewerKind>((ReviewerBox.SelectedItem as ComboBoxItem)?.Tag as string ?? nameof(ReviewerKind.Codex));
        var executor = SelectedExecutor;
        var versioning = SelectedVersioning;
        string? error = null;
        if (NXAgenticSelected) error = NXAgenticUnderConstruction;
        else if (executor == ExecutorKind.Codex ? !_codexInstalled : !_claudeInstalled)
            error = executor == ExecutorKind.Codex
                ? T("O Codex não foi encontrado nesta máquina (PATH, NXProject ou extensão do VS Code). Instale e faça login, ou escolha o Claude Code.",
                    "Codex was not found on this machine (PATH, NXProject or VS Code extension). Install it and sign in, or choose Claude Code.",
                    "Codex no se encontró en esta máquina (PATH, NXProject o extensión de VS Code). Instálelo e inicie sesión, o elija Claude Code.")
                : T("O Claude Code não foi encontrado nesta máquina. Instale e faça login, ou escolha o Codex.",
                    "Claude Code was not found on this machine. Install it and sign in, or choose Codex.",
                    "Claude Code no se encontró en esta máquina. Instálelo e inicie sesión, o elija Codex.");
        else if (versioning == VersioningMode.Local && SelectedSubject == null)
            error = T("Escolha um assunto (botão \"Assuntos…\").", "Choose a subject (\"Subjects…\" button).", "Elija un asunto (botón \"Asuntos…\").");
        else if (versioning == VersioningMode.Git && !Directory.Exists(repo)) error = T("Escolha a pasta de um repositório git.", "Choose a git repository folder.", "Elija la carpeta de un repositorio git.");
        else if (versioning == VersioningMode.Git && !Directory.Exists(Path.Combine(repo, ".git")) && !File.Exists(Path.Combine(repo, ".git")))
            error = T("A pasta não tem .git — escolha a raiz de um repositório git, ou use o versionamento local.",
                      "The folder has no .git — choose the root of a git repository, or use local versioning.",
                      "La carpeta no tiene .git — elija la raíz de un repositorio git, o use el versionado local.");
        else if (TitleBox.Text.Trim().Length == 0) error = T("Dê um título à tarefa.", "Give the task a title.", "Dé un título a la tarea.");
        else if (IntentBox.Text.Trim().Length < 10) error = T("Descreva a intenção (o que deve ser feito).", "Describe the intent (what should be done).", "Describa la intención (qué se debe hacer).");
        else if (executor == ExecutorKind.Codex && CodexModelBox.Text.Trim().Length == 0)
            error = T("Com o Codex executando, escolha um modelo da sua conta; use ↻ Modelos para atualizar a lista.", "With Codex executing, choose an account model; use ↻ Models to refresh the list.", "Con Codex como ejecutor, elija un modelo de su cuenta; use ↻ Modelos para actualizar la lista.");

        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var model = (ModelBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var codexModel = CodexModelBox.Text.Trim();
        var subject = versioning == VersioningMode.Local ? SelectedSubject : null;
        AppSettings.Update(s =>
        {
            if (subject == null) s.LastRepo = repo;
            else s.LastSubject = subject.Name;
            s.Versioning = versioning.ToString();
            s.LastModel = model;
            s.CodexModel = codexModel;
            s.Reviewer = reviewer.ToString();
            s.Executor = executor.ToString();
        });

        Result = new NewTaskInfo(subject?.Dir ?? repo, TitleBox.Text.Trim(), IntentBox.Text.Trim(),
            CriteriaBox.Text.Trim().Length > 0 ? CriteriaBox.Text.Trim() : T("(não informados)", "(not provided)", "(no informado)"),
            model.Length > 0 ? model : null, reviewer, codexModel.Length > 0 ? codexModel : null, executor, versioning,
            subject?.Name, subject?.Path);
        DialogResult = true;
    }
}
