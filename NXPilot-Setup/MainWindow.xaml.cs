// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NXPilot.Onnx;
using static NXPilot.NxText;

namespace NXPilot.Setup;

/// <summary>
/// Instalador do NXPilot. Diferente do NXProject-Setup, ainda não há release no GitHub:
/// o app vem DENTRO do Setup (mesma pasta, mesmo runtime self-contained) e o Passo 1 só
/// copia. Quando houver repositório público, o Passo 1 passa a baixar o pacote do núcleo.
/// </summary>
public partial class MainWindow : Window
{
    private const string AppExeName = "NxPilot.exe";

    private const string ClaudeInstallCommand = "irm https://claude.ai/install.ps1 | iex";
    private const string CodexInstallCommand = "npm install -g @openai/codex";

    private readonly string _installDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Apps", "NXPilot");

    private readonly OnnxModelStore _onnx = new();

    /// <summary>Pasta com os arquivos do app: a do próprio Setup, ou --payload &lt;pasta&gt; (desenvolvimento).</summary>
    private readonly string _payloadDir;

    public MainWindow()
    {
        InitializeComponent();
        _payloadDir = ResolvePayloadDir();
        VersionRun.Text = "v" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "?");
        AppPathRun.Text = T("Pasta: ", "Folder: ") + $"{_installDir}  ";
        OnnxPathRun.Text = T("Pasta: ", "Folder: ") + $"{_onnx.Folder}  ";
        ClaudeCommand.Text = ClaudeInstallCommand;
        CodexCommand.Text = CodexInstallCommand;
        LanguageBox.SelectedIndex = IsEnglish ? 1 : 0;
        Loaded += (_, _) => RefreshAll();
    }

    private void RefreshAll()
    {
        AppPathRun.Text = T("Pasta: ", "Folder: ") + $"{_installDir}  ";
        OnnxPathRun.Text = T("Pasta: ", "Folder: ") + $"{_onnx.Folder}  ";
        RefreshStep1(); RefreshStep2(); RefreshAgents(); RefreshLlama();
    }

    private void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageBox.SelectedItem is not System.Windows.Controls.ComboBoxItem { Tag: string code }) return;
        App.ApplyLanguage(code);
        RefreshAll();
    }

    private static string ResolvePayloadDir()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "--payload");
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : AppContext.BaseDirectory;
    }

    private bool PayloadPresent => File.Exists(Path.Combine(_payloadDir, AppExeName));
    private string InstalledExe => Path.Combine(_installDir, AppExeName);

    // ------------------------------------------------------------------ Passo 1: app

    private void RefreshStep1()
    {
        if (!PayloadPresent)
        {
            Step1StatusText.Text = T($"Pacote do app não encontrado em {_payloadDir}. Gere o Setup com release-nxpilot-setup.ps1.", $"App package not found in {_payloadDir}. Build the Setup with release-nxpilot-setup.ps1.");
            Step1Button.IsEnabled = false;
        }
        else if (File.Exists(InstalledExe))
        {
            var installed = FileVersionInfo.GetVersionInfo(InstalledExe).FileVersion;
            var bundled = FileVersionInfo.GetVersionInfo(Path.Combine(_payloadDir, AppExeName)).FileVersion;
            Step1StatusText.Text = installed == bundled
                ? T($"NXPilot {installed} já está instalado.", $"NXPilot {installed} is already installed.")
                : T($"Instalado: {installed} · este Setup traz: {bundled}.", $"Installed: {installed} · this Setup has: {bundled}.");
            Step1Button.Content = installed == bundled ? T("Reinstalar", "Reinstall") : T("Atualizar", "Update");
            OpenAppButton.Visibility = Visibility.Visible;
        }
        else
        {
            Step1StatusText.Text = T("Copia o NXPilot (com o runtime .NET embutido) para o seu perfil e cria um atalho na área de trabalho.", "Copies NXPilot (with the .NET runtime built in) to your profile and creates a desktop shortcut.");
            Step1Button.Content = T("Instalar", "Install");
        }
    }

    private async void OnStep1Click(object sender, RoutedEventArgs e)
    {
        Step1Button.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        Step1Progress.Value = 0;
        try
        {
            Step1StatusText.Text = T("Encerrando o NXPilot, se estiver aberto…", "Closing NXPilot, if it is open…");
            await Task.Run(StopRunningApp);

            Step1StatusText.Text = T("Copiando arquivos…", "Copying files…");
            var progress = new Progress<double>(p => Step1Progress.Value = p * 90);
            await Task.Run(() => CopyPayload(_payloadDir, _installDir, progress));
            // Exe de versões antigas (antes de se chamar NxPilot.exe). ATENÇÃO: o Windows ignora
            // maiúsculas — nunca pôr aqui "NXPilot.exe", que apagaria o NxPilot.exe recém-copiado.
            foreach (var legacy in new[] { "NXAgentic.exe", "NXAgentic.dll", "NXAgentic.deps.json", "NXAgentic.runtimeconfig.json", "NXAgentic.pdb" })
                try { File.Delete(Path.Combine(_installDir, legacy)); } catch { /* em uso: some na próxima atualização */ }
            RemoveLegacyInstall();

            // Sem o runtime ao lado do exe, o Windows mostra o diálogo genérico "instale o
            // .NET" — erro confuso. Falha aqui, apontando o que faltou.
            var missing = RequiredFiles.Where(f => !File.Exists(Path.Combine(_installDir, f))).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(T("instalação incompleta, faltam: ", "incomplete installation, missing: ") + string.Join(", ", missing));

            CreateDesktopShortcut(InstalledExe);
            Step1Progress.Value = 100;
            RefreshStep1();
            Step1StatusText.Text += T(" Atalho criado na área de trabalho.", " Desktop shortcut created.");
        }
        catch (Exception ex)
        {
            ShowError(T("Falha ao instalar: ", "Installation failed: ") + ex.Message);
            Step1StatusText.Text = T("Instalação interrompida.", "Installation interrupted.");
        }
        finally { Step1Button.IsEnabled = PayloadPresent; }
    }

    private static readonly string[] RequiredFiles =
    {
        AppExeName, "NxPilot.dll", "NXPilot.Core.dll", "NxPilot.runtimeconfig.json",
        "hostfxr.dll", "hostpolicy.dll", "coreclr.dll", "PresentationFramework.dll", "onnxruntime.dll"
    };

    // Arquivos do próprio Setup: ficam fora da pasta do app.
    private static readonly string[] SetupOwnFiles =
    {
        "NXPilot-Setup.exe", "NXPilot-Setup.dll", "NXPilot-Setup.pdb",
        "NXPilot-Setup.deps.json", "NXPilot-Setup.runtimeconfig.json"
    };

    private static void CopyPayload(string source, string dest, IProgress<double> progress)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
            .Where(f => !SetupOwnFiles.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
        for (var i = 0; i < files.Count; i++)
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, files[i]));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(files[i], target, overwrite: true);
            if (i % 20 == 0) progress.Report((double)i / files.Count);
        }
        progress.Report(1);
    }

    /// <summary>O app aberto segura DLLs e a cópia falha com "being used by another process".</summary>
    private static void StopRunningApp()
    {
        var procs = Process.GetProcessesByName("NxPilot").Concat(Process.GetProcessesByName("NXPilot")).ToArray();
        foreach (var p in procs)
        {
            try { p.Kill(true); p.WaitForExit(5000); }
            catch { /* já saiu ou sem permissão: tenta copiar mesmo assim */ }
            finally { p.Dispose(); }
        }
        if (procs.Length > 0) Thread.Sleep(500); // o SO libera os handles logo depois
    }

    /// <summary>
    /// O produto público se chamava NXAgentic (pasta %USERPROFILE%\Apps\NXAgentic e atalho
    /// NXAgentic.lnk). Remove a instalação antiga — só se for MESMO nossa (tem o NxPilot.exe) —
    /// para não ficarem dois apps. Os dados do usuário migram no primeiro início do app.
    /// </summary>
    private static void RemoveLegacyInstall()
    {
        var old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Apps", "NXAgentic");
        try { if (File.Exists(Path.Combine(old, "NxPilot.exe"))) Directory.Delete(old, recursive: true); }
        catch { /* em uso: fica para a próxima atualização */ }
        try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NXAgentic.lnk")); }
        catch { /* sem atalho antigo */ }
    }

    private static void CreateDesktopShortcut(string exePath)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var lnk = Path.Combine(desktop, "NXPilot.lnk");
            var dir = Path.GetDirectoryName(exePath) ?? "";
            static string Esc(string s) => s.Replace("'", "''");
            var script = $$"""
                $ws = New-Object -ComObject WScript.Shell
                $sc = $ws.CreateShortcut('{{Esc(lnk)}}')
                $sc.TargetPath = '{{Esc(exePath)}}'
                $sc.WorkingDirectory = '{{Esc(dir)}}'
                $sc.IconLocation = '{{Esc(exePath)}},0'
                $sc.Save()
                """;
            var ps1 = Path.Combine(Path.GetTempPath(), $"nxpilot_shortcut_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(ps1, script);
            using var proc = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{ps1}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(10_000);
            File.Delete(ps1);
        }
        catch { /* atalho é conveniência */ }
    }

    // ------------------------------------------------------------------ Passo 2: ONNX

    private void RefreshStep2()
    {
        var ok = _onnx.IsInstalled;
        Step2StatusText.Text = ok ? T("Modelo instalado.", "Model installed.") : T("Modelo ainda não baixado.", "Model not downloaded yet.");
        Step2Button.Content = ok ? T("Já instalado", "Already installed") : T("Baixar modelo", "Download model");
        Step2Button.IsEnabled = !ok;
    }

    private async void OnStep2Click(object sender, RoutedEventArgs e)
    {
        Step2Button.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            await _onnx.DownloadAsync(new Progress<string>(m => Step2ProgressText.Text = m));
        }
        catch (UnauthorizedAccessException)
        {
            ShowError(T($"Sem permissão para gravar em {_onnx.Folder}. Crie a pasta C:\\IA_LOCAL manualmente e tente de novo.", $"No permission to write to {_onnx.Folder}. Create the C:\\IA_LOCAL folder manually and try again."));
        }
        catch (Exception ex) { ShowError(T("Falha no download do modelo: ", "Model download failed: ") + ex.Message); }
        finally { RefreshStep2(); }
    }

    // ------------------------------------------------------------------ Passo 3: agentes

    private enum Where { None, Windows, Wsl }

    private void RefreshAgents()
    {
        var claude = FindOnWindows("claude");
        var codex = FindOnWindows("codex");
        SetAgentStatus(ClaudeStatus, claude ? Where.Windows : Where.None);
        SetAgentStatus(CodexStatus, codex ? Where.Windows : Where.None);
        if (!claude) _ = CheckWslAsync("claude", ClaudeStatus);
        if (!codex) _ = CheckWslAsync("codex", CodexStatus);
    }

    private void OnRecheckClick(object sender, RoutedEventArgs e) => RefreshAgents();

    /// <summary>
    /// PATH + ~/.local/bin (instalador nativo do Claude Code) + %LOCALAPPDATA%\NXProject\bin
    /// (onde o NXProject-Setup instala Codex/Claude): quem já instalou pelo NXProject não reinstala.
    /// </summary>
    private static bool FindOnWindows(string cli)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) + ";" +
                    Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) + ";" +
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin") + ";" +
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXProject", "bin"))
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] exts = { ".exe", ".cmd", ".ps1" };
        if (dirs.Any(d => exts.Any(x => File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables(d), cli + x))))) return true;

        // Binário nativo que vem nas extensões do VS Code — é o que o NXPilot usa quando não há
        // instalação no PATH (mesma busca do ClaudeCodeAdapter/CodexAdapter).
        var extensions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions");
        if (!Directory.Exists(extensions)) return false;
        return cli == "claude"
            ? Directory.EnumerateDirectories(extensions, "anthropic.claude-code-*").Any(d => File.Exists(Path.Combine(d, "resources", "native-binary", "claude.exe")))
            : Directory.EnumerateDirectories(extensions, "openai.chatgpt-*").Any(d => File.Exists(Path.Combine(d, "bin", "windows-x86_64", "codex.exe")));
    }

    private async Task CheckWslAsync(string cli, TextBlock status)
    {
        status.Text = T("verificando WSL…", "checking WSL…");
        status.Foreground = Brushes.Gray;
        var found = await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("wsl.exe", $"-- bash -lic \"command -v {cli} >/dev/null 2>&1 && echo FOUND\"")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(8000);
                return output.Contains("FOUND", StringComparison.Ordinal);
            }
            catch { return false; }
        });
        SetAgentStatus(status, found ? Where.Wsl : Where.None);
    }

    private static void SetAgentStatus(TextBlock tb, Where where)
    {
        tb.Text = where switch
        {
            Where.Windows => T("✓ instalado (Windows)", "✓ installed (Windows)"),
            Where.Wsl => T("✓ instalado (WSL)", "✓ installed (WSL)"),
            _ => T("não encontrado", "not found")
        };
        tb.Foreground = where == Where.None ? Brushes.Gray : Brushes.Green;
    }

    private void OnCopyCommand(object sender, RoutedEventArgs e)
    {
        var text = (sender as Button)?.Tag as string == "claude" ? ClaudeInstallCommand : CodexInstallCommand;
        try { Clipboard.SetText(text); } catch { /* área de transferência ocupada por outro app */ }
    }

    // ------------------------------------------------------------------ Passo 4: LLaMA

    /// <summary>Mesma checagem leve do NXProject-Setup: pasta do local-ai.json com um .gguf.</summary>
    private void RefreshLlama()
    {
        string? folder = null;
        try
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NXProject.Community", "local-ai.json");
            if (File.Exists(cfg))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(cfg));
                if (doc.RootElement.TryGetProperty("ResourcesFolder", out var f)) folder = f.GetString();
            }
        }
        catch { /* config inválida: trata como não instalada */ }
        folder ??= @"C:\IA_LOCAL";

        var ok = Directory.Exists(folder) && Directory.GetFiles(folder, "*.gguf").Length > 0
                 && File.Exists(Path.Combine(folder, "llama.dll"));
        LlamaStatus.Text = ok ? T("✓ instalada", "✓ installed") : T("não instalada", "not installed");
        LlamaStatus.Foreground = ok ? Brushes.Green : Brushes.Gray;
        LlamaPathText.Text = T("Pasta: ", "Folder: ") + folder;
    }

    // ------------------------------------------------------------------ comum

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(InstalledExe) { WorkingDirectory = _installDir, UseShellExecute = true });
            Close();
        }
        catch (Exception ex) { ShowError(T("Não foi possível abrir o NXPilot: ", "Could not open NXPilot: ") + ex.Message); }
    }

    private void OnOpenAppFolder(object sender, RoutedEventArgs e) => OpenFolder(_installDir);
    private void OnOpenOnnxFolder(object sender, RoutedEventArgs e) => OpenFolder(_onnx.Folder);
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { /* conveniência */ }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
