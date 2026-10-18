// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NXPilot.Desktop.ViewModels;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Tutorial;

/// <summary>
/// Tour guiado sobre a tela de verdade: escurece tudo menos o elemento do passo (que segue
/// clicável), desenha uma moldura em volta e posiciona o cartão ao lado dele.
/// Teclado: → / Enter avança, ← volta, Esc sai.
/// </summary>
public partial class TourOverlay : UserControl
{
    private const double Gap = 14, Edge = 12, HolePadding = 6;

    private static readonly Brush DotCurrent = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x9A)));
    private static readonly Brush DotDone = Freeze(new SolidColorBrush(Color.FromRgb(0x9D, 0xB4, 0xD6)));
    private static readonly Brush DotTodo = Freeze(new SolidColorBrush(Color.FromRgb(0xDD, 0xE1, 0xE6)));

    private Lesson? _lesson;
    private int _index;
    private Window? _host;
    private MainViewModel? _vm;
    private FrameworkElement? _target;

    /// <summary>Fim da lição: true = chegou ao último passo e clicou Concluir; false = saiu antes.</summary>
    public event Action<Lesson, bool>? Ended;

    public TourOverlay()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Layout();
        KeyDown += OnKeyDown;
    }

    public bool IsRunning => _lesson != null;

    public void Start(Lesson lesson, Window host, MainViewModel vm, int step = 0)
    {
        _lesson = lesson;
        _host = host;
        _vm = vm;
        Visibility = Visibility.Visible;
        _ = ShowStepAsync(Math.Clamp(step, 0, lesson.Steps.Count - 1));
    }

    private async Task ShowStepAsync(int index)
    {
        if (_lesson == null || _host == null) return;
        _index = index;
        var step = _lesson.Steps[index];

        if (step.Tab is int tab && _host.FindName("MainTabs") is TabControl tabs)
            tabs.SelectedIndex = tab;
        // Espera a aba trocar e o layout assentar antes de medir o alvo.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        _target = step.Target != null ? _host.FindName(step.Target) as FrameworkElement : null;
        if (_target != null)
        {
            _target.BringIntoView();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        }

        var last = index == _lesson.Steps.Count - 1;
        LessonTitle.Text = _lesson.Title.ToUpperInvariant();
        StepCounter.Text = T($"Passo {index + 1} de {_lesson.Steps.Count}", $"Step {index + 1} of {_lesson.Steps.Count}");
        StepTitle.Text = step.Title;
        StepBody.Text = step.Body;
        TryButton.Content = "▶  " + step.TryLabel;
        TryButton.Visibility = step.Try != null ? Visibility.Visible : Visibility.Collapsed;
        TryButton.IsEnabled = true;
        TryDone.Text = T("✓ Feito — veja o resultado na área destacada.", "✓ Done — see the result in the highlighted area.");
        TryDone.Visibility = Visibility.Collapsed;
        BackButton.IsEnabled = index > 0;
        NextButton.Content = last ? T("Concluir lição ✓", "Finish lesson ✓") : T("Próximo →", "Next →");
        Dots.ItemsSource = Enumerable.Range(0, _lesson.Steps.Count)
            .Select(i => i == index ? DotCurrent : i < index ? DotDone : DotTodo).ToList();

        Layout();
        Focus();
    }

    /// <summary>Recalcula o buraco, a moldura e a posição do cartão (passo novo ou janela redimensionada).</summary>
    private void Layout()
    {
        if (_lesson == null || ActualWidth <= 0) return;
        var full = new Rect(0, 0, ActualWidth, ActualHeight);

        Rect? hole = null;
        if (_target is { IsVisible: true, ActualWidth: > 0 })
        {
            var r = _target.TransformToVisual(this).TransformBounds(new Rect(_target.RenderSize));
            r.Inflate(HolePadding, HolePadding);
            r.Intersect(full);
            if (!r.IsEmpty) hole = r;
        }

        if (hole is Rect h)
        {
            Dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(h, 8, 8));
            Highlight.Visibility = Visibility.Visible;
            Highlight.Margin = new Thickness(h.X, h.Y, 0, 0);
            Highlight.Width = h.Width;
            Highlight.Height = h.Height;
        }
        else
        {
            Dim.Data = new RectangleGeometry(full);
            Highlight.Visibility = Visibility.Collapsed;
        }

        Card.Measure(new Size(Card.Width, double.PositiveInfinity));
        var (x, y) = PlaceCard(full, hole, Card.Width, Card.DesiredSize.Height);
        Canvas.SetLeft(Card, x);
        Canvas.SetTop(Card, y);
    }

    /// <summary>Direita do alvo → esquerda → abaixo → acima → centro, sempre dentro da janela.</summary>
    private static (double X, double Y) PlaceCard(Rect full, Rect? hole, double w, double h)
    {
        static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(v, max));
        var maxX = full.Width - w - Edge;
        var maxY = full.Height - h - Edge;
        if (hole is not Rect r) return ((full.Width - w) / 2, (full.Height - h) / 2);

        if (r.Right + Gap + w <= full.Width - Edge) return (r.Right + Gap, Clamp(r.Top, Edge, maxY));
        if (r.Left - Gap - w >= Edge) return (r.Left - Gap - w, Clamp(r.Top, Edge, maxY));
        if (r.Bottom + Gap + h <= full.Height - Edge) return (Clamp(r.Left, Edge, maxX), r.Bottom + Gap);
        if (r.Top - Gap - h >= Edge) return (Clamp(r.Left, Edge, maxX), r.Top - Gap - h);
        // Alvo do tamanho da tela (ex.: painel grande): cartão no canto inferior direito, por cima.
        return (maxX, maxY);
    }

    private async void OnTryClick(object sender, RoutedEventArgs e)
    {
        if (_lesson?.Steps[_index].Try is not { } action || _vm == null) return;
        TryButton.IsEnabled = false;
        try
        {
            await action(_vm);
            TryDone.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            TryDone.Text = T("Não deu certo: ", "It did not work: ") + ex.Message;
            TryDone.Visibility = Visibility.Visible;
        }
        finally { TryButton.IsEnabled = true; Layout(); }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_lesson == null) return;
        if (_index < _lesson.Steps.Count - 1) _ = ShowStepAsync(_index + 1);
        else Finish(completed: true);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_index > 0) _ = ShowStepAsync(_index - 1);
    }

    private void OnSkipClick(object sender, RoutedEventArgs e) => Finish(completed: false);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right or Key.Enter: OnNextClick(this, e); e.Handled = true; break;
            case Key.Left: OnBackClick(this, e); e.Handled = true; break;
            case Key.Escape: Finish(completed: false); e.Handled = true; break;
        }
    }

    private void Finish(bool completed)
    {
        var lesson = _lesson;
        _lesson = null;
        _target = null;
        Visibility = Visibility.Collapsed;
        if (lesson != null) Ended?.Invoke(lesson, completed);
    }

    private static Brush Freeze(Brush b)
    {
        b.Freeze();
        return b;
    }
}
