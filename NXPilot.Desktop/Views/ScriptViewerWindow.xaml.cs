// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using System.Windows;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Views;

/// <summary>
/// Leitura do script que um pedido do agente vai executar. Só LÊ o arquivo como texto —
/// abrir pelo programa padrão do Windows executaria um .py/.bat em vez de mostrar.
/// </summary>
public partial class ScriptViewerWindow : Window
{
    private const long MaxBytes = 2 * 1024 * 1024;

    public ScriptViewerWindow(string action, IReadOnlyList<string> scripts)
    {
        InitializeComponent();
        ActionText.Text = action;
        HintText.Text = T("Somente leitura. O agente continua esperando a sua decisão no painel de Decisões.",
                          "Read-only. The agent is still waiting for your decision in the Decisions panel.",
                          "Solo lectura. El agente sigue esperando su decisión en el panel de Decisiones.");
        FileBox.ItemsSource = scripts;
        FileBox.Visibility = scripts.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        FileBox.SelectedIndex = 0; // dispara OnFileChanged, que mostra o primeiro
    }

    private void OnFileChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FileBox.SelectedItem is string path) ShowFile(path);
    }

    private void ShowFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                ContentBox.Text = T("(arquivo grande demais para mostrar aqui)", "(file too large to show here)", "(archivo demasiado grande para mostrar aquí)");
                InfoText.Text = path;
                return;
            }
            var lines = File.ReadAllLines(path);
            var width = lines.Length.ToString().Length;
            ContentBox.Text = string.Join(Environment.NewLine, lines.Select((l, i) => $"{(i + 1).ToString().PadLeft(width)}  {l}"));
            InfoText.Text = $"{path} · {lines.Length} {T("linhas", "lines", "líneas")} · {info.LastWriteTime:dd/MM HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ContentBox.Text = ex.Message;
            InfoText.Text = path;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
