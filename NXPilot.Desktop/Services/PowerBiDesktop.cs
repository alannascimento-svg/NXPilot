// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.IO;
using NXPilot.WindowsUi;

namespace NXPilot.Desktop.Services;

/// <summary>
/// Extensão Power BI Desktop, parte de INTERFACE (pacote NXPilot.WindowsUi): abre o .pbip no
/// Power BI Desktop e captura a janela para conferir o resultado. Não clica em nada e não monta
/// visual — isso o agente faz editando TMDL/PBIR. Só enxerga o processo PBIDesktop.
/// Testado em 18/10/2026 com o Power BI da Microsoft Store (2.159).
/// </summary>
public static class PowerBiDesktop
{
    public const string ProcessName = "PBIDesktop";

    /// <summary>Instalação clássica (Program Files) ou alias da versão da Microsoft Store.</summary>
    public static string? Locate()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Power BI Desktop", "bin", "PBIDesktop.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "PBIDesktopStore.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Abre o projeto, espera a janela (o título é só o nome do projeto — o Power BI é reconhecido
    /// pelo processo; até 4 min, o Power BI demora a carregar o modelo), dá tempo para os visuais
    /// renderizarem e grava a captura em <paramref name="png"/>. O Power BI continua aberto.
    /// </summary>
    public static async Task<string> OpenAndCaptureAsync(string pbipPath, string png, CancellationToken ct = default)
    {
        var exe = Locate() ?? throw new InvalidOperationException(NxText.T(
            "Power BI Desktop não encontrado. Instale pela Microsoft Store ou em https://powerbi.microsoft.com/desktop.",
            "Power BI Desktop not found. Install it from the Microsoft Store or https://powerbi.microsoft.com/desktop."));
        using var ui = new WindowsUiService(WindowsUiPolicy.PowerBiOnly);
        WindowInfo window;
        try
        {
            window = await ui.LaunchAndWaitAsync(exe, $"\"{pbipPath}\"", ProcessName,
                Path.GetFileNameWithoutExtension(pbipPath), TimeSpan.FromMinutes(4), ct);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(NxText.T("O Power BI Desktop não mostrou a janela do projeto em 4 minutos.", "Power BI Desktop did not show the project window within 4 minutes."));
        }
        await Task.Delay(TimeSpan.FromSeconds(10), ct); // modelo carregado → visuais renderizando
        return ui.Capture(window, png);
    }
}
