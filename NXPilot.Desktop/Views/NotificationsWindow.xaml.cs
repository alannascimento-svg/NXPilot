// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using NXPilot.Desktop.ViewModels;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>Lista de avisos; abrir marca todos como lidos.</summary>
public partial class NotificationsWindow : Window
{
    private readonly NotificationCenter _center;

    public NotificationsWindow(NotificationCenter center)
    {
        InitializeComponent();
        _center = center;
        Title = Heading.Text = T("Notificações", "Notifications");
        ClearButton.Content = T("Limpar", "Clear");
        CloseButton.Content = T("Fechar", "Close");
        EmptyText.Text = T("Nenhum aviso por enquanto. Decisões pendentes, planos prontos e tarefas concluídas aparecem aqui.",
                           "No notifications yet. Pending decisions, ready plans and finished tasks show up here.");
        List.ItemsSource = center.Items;
        EmptyText.Visibility = center.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Closed += (_, _) => center.MarkAllRead();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _center.Clear();
        EmptyText.Visibility = Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
