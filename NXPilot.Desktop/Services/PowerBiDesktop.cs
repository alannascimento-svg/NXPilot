// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.IO;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;

namespace NXPilot.Desktop.Services;

/// <summary>
/// Parte de INTERFACE do Power BI (FlaUI, MIT): abre o .pbip no Power BI Desktop e captura a
/// janela para conferir o resultado. Não clica em nada e não monta visual — isso o agente
/// faz editando TMDL/PBIR. Ainda não testado numa máquina com o Power BI Desktop instalado.
/// </summary>
public static class PowerBiDesktop
{
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
    /// Abre o projeto, espera a janela principal (título com o nome do projeto, até 4 min —
    /// o Power BI demora a carregar o modelo), dá tempo para os visuais renderizarem e grava a
    /// captura em <paramref name="png"/>. O Power BI continua aberto para a pessoa conferir.
    /// </summary>
    public static async Task<string> OpenAndCaptureAsync(string pbipPath, string png, CancellationToken ct = default)
    {
        var exe = Locate() ?? throw new InvalidOperationException(NxText.T(
            "Power BI Desktop não encontrado. Instale pela Microsoft Store ou em https://powerbi.microsoft.com/desktop.",
            "Power BI Desktop not found. Install it from the Microsoft Store or https://powerbi.microsoft.com/desktop."));
        Process.Start(new ProcessStartInfo(exe, $"\"{pbipPath}\"") { UseShellExecute = true });

        var name = Path.GetFileNameWithoutExtension(pbipPath);
        using var automation = new UIA3Automation();
        var deadline = DateTime.Now.AddMinutes(4);
        FlaUI.Core.AutomationElements.AutomationElement? window = null;
        while (window == null && DateTime.Now < deadline)
        {
            ct.ThrowIfCancellationRequested();
            // A versão da Store abre por outro processo: procura pela janela no desktop, não pelo PID.
            // O título é só o nome do projeto (sem "Power BI"); o processo é que identifica o Power BI.
            window = automation.GetDesktop().FindAllChildren()
                .FirstOrDefault(w => (w.Properties.Name.ValueOrDefault ?? "").Contains(name, StringComparison.OrdinalIgnoreCase)
                                     && IsPowerBiProcess(w.Properties.ProcessId.ValueOrDefault));
            if (window == null) await Task.Delay(2000, ct);
        }
        if (window == null)
            throw new TimeoutException(NxText.T("O Power BI Desktop não mostrou a janela do projeto em 4 minutos.", "Power BI Desktop did not show the project window within 4 minutes."));

        await Task.Delay(TimeSpan.FromSeconds(10), ct); // modelo carregado → visuais renderizando
        Directory.CreateDirectory(Path.GetDirectoryName(png)!);
        Capture.Element(window).ToFile(png);
        return png;
    }

    private static bool IsPowerBiProcess(int pid)
    {
        try { return Process.GetProcessById(pid).ProcessName.StartsWith("PBIDesktop", StringComparison.OrdinalIgnoreCase); }
        catch (ArgumentException) { return false; } // processo já saiu
    }
}
