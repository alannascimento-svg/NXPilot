// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

public enum CloseChoice { Cancel, Minimize, ExitAndStopAgents }

/// <summary>
/// Pergunta ao fechar com agentes em execução: fechar a janela sem querer não pode matar um
/// agente no meio de uma fase (worktree pela metade, push interrompido).
/// </summary>
public partial class CloseDialog : Window
{
    public CloseChoice Choice { get; private set; } = CloseChoice.Cancel;

    public CloseDialog(int runningAgents)
    {
        InitializeComponent();
        Title = "NXPilot";
        Heading.Text = T("Fechar o NXPilot?", "Close NXPilot?");
        Body.Text = runningAgents == 1
            ? T("Há 1 agente em execução.", "1 agent is running.")
            : T($"Há {runningAgents} agentes em execução.", $"{runningAgents} agents are running.");
        MinimizeButton.Content = T("Minimizar — os agentes continuam", "Minimize — agents keep running");
        MinimizeHint.Text = T("A janela vai para a barra de tarefas; as decisões pendentes esperam por você.",
                              "The window goes to the taskbar; pending decisions wait for you.");
        ExitButton.Content = T("Finalizar os agentes e sair", "Stop the agents and exit");
        ExitHint.Text = T("Cada agente é encerrado e registrado no log; o trabalho fica no worktree para retomar depois.",
                          "Each agent is stopped and logged; the work stays in its worktree to resume later.");
        CancelButton.Content = T("Cancelar", "Cancel");
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.Minimize;
        Close();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.ExitAndStopAgents;
        Close();
    }
}
