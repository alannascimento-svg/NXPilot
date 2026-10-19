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
        Title = Heading.Text = T("Work items · Azure DevOps", "Work items · Azure DevOps", "Work items · Azure DevOps");
        SubHeading.Text = _connection == null
            ? T("Conexão não encontrada. Configure o Azure DevOps no NXProject (organização, projeto e PAT) — o NXPilot usa a mesma conexão.",
                "Connection not found. Set up Azure DevOps in NXProject (organization, project and PAT) — NXPilot uses the same connection.",
                "Conexión no encontrada. Configure Azure DevOps en NXProject (organización, proyecto y PAT) — NXPilot usa la misma conexión.")
            : T($"Itens abertos atribuídos a você em {_connection.Project} · conexão do NXProject, somente leitura.",
                $"Open items assigned to you in {_connection.Project} · NXProject connection, read-only.",
                $"Elementos abiertos asignados a usted en {_connection.Project} · conexión de NXProject, solo lectura.");
        ColId.Header = "ID";
        ColType.Header = T("Tipo", "Type", "Tipo");
        ColTitle.Header = T("Título", "Title", "Título");
        ColState.Header = T("Estado", "State", "Estado");
        DetailCriteriaLabel.Text = T("CRITÉRIOS DE ACEITE", "ACCEPTANCE CRITERIA", "CRITERIOS DE ACEPTACIÓN");
        RefreshButton.Content = T("Atualizar", "Refresh", "Actualizar");
        OpenWebButton.Content = T("Abrir no DevOps", "Open in DevOps", "Abrir en DevOps");
        CreateButton.Content = T("Criar tarefa a partir deste item ▶", "Create task from this item ▶", "Crear tarea a partir de este elemento ▶");
        CloseButton.Content = T("Fechar", "Close", "Cerrar");
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        CreateButton.IsEnabled = OpenWebButton.IsEnabled = false;
        if (_connection == null) { RefreshButton.IsEnabled = false; return; }
        StatusText.Text = T("Consultando o Azure DevOps…", "Querying Azure DevOps…", "Consultando Azure DevOps…");
        try
        {
            using var client = new AzureDevOpsClient(_connection);
            var items = await client.MyOpenWorkItemsAsync();
            Grid.ItemsSource = items;
            StatusText.Text = T($"{items.Count} item(ns).", $"{items.Count} item(s).", $"{items.Count} elemento(s).");
            if (items.Count > 0) Grid.SelectedIndex = 0;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var w = Grid.SelectedItem as WorkItem;
        CreateButton.IsEnabled = OpenWebButton.IsEnabled = w != null;
        DetailTitle.Text = w == null ? "" : $"#{w.Id} · {w.Title}";
        DetailDescription.Text = w == null ? "" : w.Description.Length > 0 ? w.Description : T("(sem descrição)", "(no description)", "(sin descripción)");
        DetailCriteria.Text = w == null ? "" : w.AcceptanceCriteria.Length > 0 ? w.AcceptanceCriteria : T("(sem critérios de aceite)", "(no acceptance criteria)", "(sin criterios de aceptación)");
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
