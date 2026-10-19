// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace NXPilot.Planning;

/// <summary>
/// Acha e CONFERE o módulo comercial (NXAgentic.Intelligence.exe). Uma pasta de "módulos" é
/// uma porta de entrada: sem conferência, qualquer exe com esse nome rodaria com o contexto
/// das tarefas. Por isso o módulo só é usado se (1) o Windows validar a assinatura Authenticode
/// — arquivo íntegro, não alterado — e (2) o certificado for um dos da Nexus XData, fixados
/// aqui pela impressão digital. Qualquer falha: NXPilot segue com o planejador Basic.
/// </summary>
public static class IntelligenceModule
{
    public const string ExeName = "NXAgentic.Intelligence.exe";

    /// <summary>Certificados de assinatura aceitos (SHA-1 thumbprint). Público de propósito: identifica, não protege.</summary>
    public static readonly string[] TrustedThumbprints =
    {
        "99CCBA0CD1CAFCF53F0641D9B87DD32775339670", // CN=NXProject Dev Local (desenvolvimento, Nexus XData)
    };

    /// <summary>Onde procurar: pasta "modules" ao lado do NxPilot.exe e em %LOCALAPPDATA%\NXPilot\modules.</summary>
    public static IEnumerable<string> Candidates() => new[]
    {
        Path.Combine(AppContext.BaseDirectory, "modules", ExeName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXPilot", "modules", ExeName),
    };

    /// <summary>Planejador a usar: o do NXAgentic se instalado e confiável; senão o Basic. <paramref name="status"/> explica a escolha.</summary>
    public static IAgentPlanner CreatePlanner(Action<string>? log, out string status)
    {
        foreach (var path in Candidates().Where(File.Exists))
        {
            if (!OperatingSystem.IsWindows()) break;
            var check = Verify(path);
            if (check == null)
            {
                status = NxText.T($"NXAgentic Intelligence ativo ({path}).", $"NXAgentic Intelligence active ({path}).", $"NXAgentic Intelligence activo ({path}).");
                return new IntelligencePlanner(new ProcessIntelligenceTransport(path), log);
            }
            status = NxText.T($"Módulo ignorado: {path} — {check}. Usando o planejador Basic.", $"Module ignored: {path} — {check}. Using the Basic planner.", $"Módulo ignorado: {path} — {check}. Usando el planificador Basic.");
            return new BasicAgentPlanner();
        }
        status = NxText.T("Planejador Basic (NXPilot Community).", "Basic planner (NXPilot Community).", "Planificador Basic (NXPilot Community).");
        return new BasicAgentPlanner();
    }

    /// <summary>Nulo = confiável; senão, o motivo da recusa.</summary>
    [SupportedOSPlatform("windows")]
    public static string? Verify(string path)
    {
        var trust = WinTrust.Verify(path);
        if (trust != 0) return NxText.T($"assinatura inválida ou ausente (0x{trust:X8})", $"invalid or missing signature (0x{trust:X8})", $"firma no válida o ausente (0x{trust:X8})");
        try
        {
#pragma warning disable SYSLIB0057 // a API nova (X509CertificateLoader) não lê o certificado de um exe assinado
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return TrustedThumbprints.Contains(cert.Thumbprint, StringComparer.OrdinalIgnoreCase)
                ? null
                : NxText.T($"assinado por quem não é a Nexus XData ({cert.Subject})", $"signed by someone other than Nexus XData ({cert.Subject})", $"firmado por alguien que no es Nexus XData ({cert.Subject})");
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>WinVerifyTrust: o mesmo teste que o Windows faz ao abrir um exe assinado (sem janelas, sem rede).</summary>
    [SupportedOSPlatform("windows")]
    private static class WinTrust
    {
        private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileInfo
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Data
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;          // 2 = WTD_UI_NONE
            public uint fdwRevocationChecks; // 0 = WTD_REVOKE_NONE
            public uint dwUnionChoice;       // 1 = WTD_CHOICE_FILE
            public IntPtr pFile;
            public uint dwStateAction;       // 0 = WTD_STATEACTION_IGNORE
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;         // 0x1000 = WTD_CACHE_ONLY_URL_RETRIEVAL (sem rede)
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref Data data);

        public static int Verify(string path)
        {
            var file = new FileInfo { cbStruct = (uint)Marshal.SizeOf<FileInfo>(), pcwszFilePath = path };
            var filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
            try
            {
                Marshal.StructureToPtr(file, filePtr, false);
                var data = new Data
                {
                    cbStruct = (uint)Marshal.SizeOf<Data>(),
                    dwUIChoice = 2,
                    dwUnionChoice = 1,
                    pFile = filePtr,
                    dwProvFlags = 0x1000,
                };
                var action = GenericVerifyV2;
                return WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            }
            finally
            {
                Marshal.DestroyStructure<FileInfo>(filePtr);
                Marshal.FreeHGlobal(filePtr);
            }
        }
    }
}
