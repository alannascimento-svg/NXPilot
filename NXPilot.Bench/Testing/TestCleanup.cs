// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Testing;

internal static class TestCleanup
{
    public static async Task DeleteFlatDirectoryAsync(string folder)
    {
        for (var attempt = 0; Directory.Exists(folder); attempt++)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
                return;
            }
            // O Windows pode concluir a exclusão dos arquivos após File.Delete retornar.
            catch (IOException) when (attempt < 4) { await Task.Delay(100); }
        }
    }
}
