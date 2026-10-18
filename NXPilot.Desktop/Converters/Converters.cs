// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NXPilot.Risk;

namespace NXPilot.Desktop.Converters;

/// <summary>
/// Cor por chave semântica (risco, gravidade, status). Parâmetro "fg" = texto, senão fundo.
/// Uma tabela só, para que risco ALTO e verificação Crítica falem a mesma língua visual.
/// </summary>
public sealed class SemanticBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, (string Bg, string Fg)> Palette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alto"] = ("#FDECEA", "#B3261E"),
        ["Critico"] = ("#FDECEA", "#B3261E"),
        ["Medio"] = ("#FFF4E5", "#9A5B00"),
        ["Maior"] = ("#FFF4E5", "#9A5B00"),
        ["Baixo"] = ("#EAF4EC", "#1E6B34"),
        ["Menor"] = ("#EEF2F8", "#2B579A"),
        ["Rodando"] = ("#EAF4EC", "#217346"),
        ["Revisando"] = ("#EEF2F8", "#2B579A"),
        ["Aguardando"] = ("#FFF4E5", "#9A5B00"),
        ["Concluido"] = ("#F0F0F0", "#666666"),
        ["Pendente"] = ("#F0F0F0", "#666666"),
        ["Prioritario"] = ("#FDECEA", "#B3261E"),
        ["Relevante"] = ("#EEF2F8", "#2B579A"),
        ["Ignorar"] = ("#F0F0F0", "#999999"),
        ["Add"] = ("#EAF4EC", "#1E6B34"),
        ["Del"] = ("#FDECEA", "#B3261E"),
        ["Hunk"] = ("#EEF2F8", "#2B579A"),
    };

    private static readonly Dictionary<string, Brush> Cache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString() ?? "";
        var fg = "fg".Equals(parameter as string, StringComparison.OrdinalIgnoreCase);
        if (!Palette.TryGetValue(key, out var pair)) return fg ? Brushes.Black : Brushes.Transparent;
        var hex = fg ? pair.Fg : pair.Bg;
        if (!Cache.TryGetValue(hex, out var brush))
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            Cache[hex] = brush;
        }
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Rótulo exibido do risco (com acento), sem mudar o enum.</summary>
public sealed class RiskLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RiskLevel.Alto => NxText.T("RISCO ALTO", "HIGH RISK"),
        RiskLevel.Medio => NxText.T("RISCO MÉDIO", "MEDIUM RISK"),
        RiskLevel.Baixo => NxText.T("RISCO BAIXO", "LOW RISK"),
        _ => NxText.T("classificando…", "classifying…")
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Nível de risco como palavra no idioma ativo (grades do Laboratório).</summary>
public sealed class RiskWordConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RiskLevel.Alto => NxText.T("ALTO", "HIGH"),
        RiskLevel.Medio => NxText.T("MÉDIO", "MEDIUM"),
        RiskLevel.Baixo => NxText.T("BAIXO", "LOW"),
        _ => value?.ToString() ?? ""
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>bool → Visible/Collapsed; parâmetro "not" inverte.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true || (value is int i && i > 0) || (value is string s && s.Length > 0);
        if ("not".Equals(parameter as string)) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Visível quando há valor (ex.: um motor selecionado na comparação).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class CheckMarkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? "✓" : "✗";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
