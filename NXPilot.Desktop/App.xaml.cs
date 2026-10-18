// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Windows;
using NXPilot.Desktop.Services;

namespace NXPilot.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        NXPilot.LegacyDataMigration.Run(); // dados do antigo NXAgentic; antes de ler settings.json (o idioma salvo mora lá)
        NXPilot.Updates.UpdateService.CleanupOldFiles(AppContext.BaseDirectory); // *.nxold da atualização anterior
        // Antes de qualquer janela: os textos já nascem no idioma salvo (ou o do Windows).
        // --lang en-US força o idioma só nesta execução (prints, testes) sem gravar a escolha.
        var i = Array.IndexOf(e.Args, "--lang");
        LanguageService.Apply(i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : LanguageService.LoadPreferred());
        base.OnStartup(e);
    }
}
