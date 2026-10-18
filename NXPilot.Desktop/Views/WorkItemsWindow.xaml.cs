// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.Windows;
using NXPilot.Integrations;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Work items do Azure DevOps atribuídos a mim (conexão do NXProject, somente leitura).
/// "Criar tarefa" devolve o item escolhido; quem abre a Nova tarefa preenchida é a MainWindow.
/// </summary>
public partial class WorkItemsWindow : Window
{
    private readonly DevOpsConnection? _connection = DevOpsConnection.FromNXProject();

    public WorkItem? Selected { get; private set; }

    public WorkItemsWindow()
    {
        InitializeComponent();
        Title = Heading.Text = T("Work items · Azure DevOps", "Work items · Azure DevOps");
        SubHeading.Text = _connection == null
            ? T("Conexão não encontrada. Configure o Azure DevOps no NXProject (organização, projeto e PAT) — o NXPilot usa a mesma conexão.",
                "Connection not found. Set up Azure DevOps in NXProject (organization, project and PAT) — NXPilot uses the same connection.")
            : T($"Itens abertos atribuídos a você em {_connection.Project} · conexão do NXProject, somente leitura.",
                $"Open items assigned to you in {_connection.Project} · NXProject connection, read-only.");
        ColId.Header = "ID";
        ColType.Header = T("Tipo", "Type");
        ColTitle.Header = T("Título", "Title");
        ColState.Header = T("Estado", "State");
        DetailCriteriaLabel.Text = T("CRITÉRIOS DE ACEITE", "ACCEPTANCE CRITERIA");
        RefreshButton.Content = T("Atualizar", "Refresh");
        OpenWebButton.Content = T("Abrir no DevOps", "Open in DevOps");
        CreateButton.Content = T("Criar tarefa a partir deste item ▶", "Create task from this item ▶");
        CloseButton.Content = T("Fechar", "Close");
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        CreateButton.IsEnabled = OpenWebButton.IsEnabled = false;
        if (_connection == null) { RefreshButton.IsEnabled = false; return; }
        StatusText.Text = T("Consultando o Azure DevOps…", "Querying Azure DevOps…");
        try
        {
            using var client = new AzureDevOpsClient(_connection);
            var items = await client.MyOpenWorkItemsAsync();
            Grid.ItemsSource = items;
            StatusText.Text = T($"{items.Count} item(ns).", $"{items.Count} item(s).");
            if (items.Count > 0) Grid.SelectedIndex = 0;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var w = Grid.SelectedItem as WorkItem;
        CreateButton.IsEnabled = OpenWebButton.IsEnabled = w != null;
        DetailTitle.Text = w == null ? "" : $"#{w.Id} · {w.Title}";
        DetailDescription.Text = w == null ? "" : w.Description.Length > 0 ? w.Description : T("(sem descrição)", "(no description)");
        DetailCriteria.Text = w == null ? "" : w.AcceptanceCriteria.Length > 0 ? w.AcceptanceCriteria : T("(sem critérios de aceite)", "(no acceptance criteria)");
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private void OnOpenWebClick(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is WorkItem w) Process.Start(new ProcessStartInfo(w.Url) { UseShellExecute = true });
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not WorkItem w) return;
        Selected = w;
        Close();
    }
}
