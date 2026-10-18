// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Tutorial;

/// <summary>
/// Hub das lições: lista com progresso, "Começar"/"Rever" e, logo depois de concluir uma
/// lição, o atalho para a próxima. Devolve em <see cref="SelectedLesson"/> a lição escolhida;
/// quem abriu (MainWindow) roda o tour.
/// </summary>
public partial class HelpWindow : Window
{
    private sealed record LessonRow(Lesson Lesson, string Badge, Brush BadgeBrush, string Meta, string ButtonText);

    private static readonly Brush DoneBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x73, 0x46));
    private static readonly Brush TodoBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x9A));

    private readonly TutorialProgress _progress;

    public Lesson? SelectedLesson { get; private set; }

    public HelpWindow(TutorialProgress progress, Lesson? justCompleted = null)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        _progress = progress;
        ShowOnStartupCheck.IsChecked = progress.ShowOnStartup;
        Refresh(justCompleted);
        // Fechar pelo X também guarda a escolha do "mostrar ao abrir".
        Closing += (_, _) =>
        {
            _progress.ShowOnStartup = ShowOnStartupCheck.IsChecked == true;
            _progress.Save();
        };
    }

    private void Refresh(Lesson? justCompleted)
    {
        var all = LessonCatalog.All;
        var done = all.Count(l => _progress.Completed.Contains(l.Id));
        var next = _progress.NextLesson();

        Heading.Text = done == 0 ? T("Bem-vindo ao NXPilot", "Welcome to NXPilot")
            : done == all.Count ? T("Você concluiu as lições básicas", "You have completed the basic lessons")
            : T("Continue de onde parou", "Pick up where you left off");
        SubHeading.Text = done == 0
            ? T($"{all.Count} lições curtas, direto na tela do app. Comece pela primeira — leva {all[0].Minutes} minutos.",
                $"{all.Count} short lessons, right on the app screen. Start with the first one — it takes {all[0].Minutes} minutes.")
            : done == all.Count
                ? T("Pode rever qualquer lição quando quiser.", "You can review any lesson whenever you want.")
                : T($"Próxima sugerida: {next!.Title} ({next.Minutes} min).", $"Suggested next: {next!.Title} ({next.Minutes} min).");

        ProgressText.Text = T($"{done} de {all.Count} concluídas", $"{done} of {all.Count} completed");
        Progress.Value = (double)done / all.Count;

        if (justCompleted != null)
        {
            DoneBanner.Visibility = Visibility.Visible;
            // Título no idioma ATUAL (a lição concluída pode ter sido feita no outro idioma).
            var doneTitle = all.FirstOrDefault(l => l.Id == justCompleted.Id)?.Title ?? justCompleted.Title;
            DoneText.Text = T($"✓ Lição concluída: {doneTitle}", $"✓ Lesson completed: {doneTitle}");
            NextLessonButton.Visibility = next != null ? Visibility.Visible : Visibility.Collapsed;
            NextLessonButton.Content = next != null ? T($"Próxima: {next.Title} →", $"Next: {next.Title} →") : "";
            NextLessonButton.Tag = next;
        }
        else DoneBanner.Visibility = Visibility.Collapsed;

        LessonList.ItemsSource = all.Select((l, i) =>
        {
            var isDone = _progress.Completed.Contains(l.Id);
            // Compara por Id: o catálogo é montado de novo a cada acesso (segue o idioma).
            return new LessonRow(l, isDone ? "✓" : (i + 1).ToString(), isDone ? DoneBrush : TodoBrush,
                T($"{l.Steps.Count} passos · {l.Minutes} min", $"{l.Steps.Count} steps · {l.Minutes} min"),
                isDone ? T("Rever", "Review") : l.Id == next?.Id ? T("Começar ▶", "Start ▶") : T("Começar", "Start"));
        }).ToList();
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not Lesson lesson) return;
        SelectedLesson = lesson;
        Close();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _progress.Completed.Clear();
        _progress.Save();
        Refresh(null);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
