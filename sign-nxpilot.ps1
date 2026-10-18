param(
    [switch]$RecreateCertificate
)

# Nao precisa executar como Administrador.
# Assina os binarios locais do NXPilot com o MESMO certificado de desenvolvimento do
# NXProject ("CN=NXProject Dev Local"): ele ja esta confiavel no perfil, entao o Windows
# (Smart App Control / politicas locais) trata os dois produtos da Nexus XData igual, sem
# um segundo certificado para instalar. Se ainda nao existir, e criado aqui.

$CertSubject = "CN=NXProject Dev Local"
$ProjectBin  = @(
    (Join-Path $PSScriptRoot "NXPilot.Desktop\bin\Debug\net10.0-windows")
    (Join-Path $PSScriptRoot "NXPilot.Desktop\bin\Release\net10.0-windows")
    (Join-Path $PSScriptRoot "NXPilot-Setup\bin\Debug\net10.0-windows\win-x64")
    (Join-Path $PSScriptRoot "NXPilot-Setup\bin\Release\net10.0-windows\win-x64")
    (Join-Path $PSScriptRoot "dist\setup\publish-win-x64")
)

function Open-CurrentUserStore([string]$StoreName, [string]$OpenFlags = "ReadWrite") {
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($StoreName, "CurrentUser")
    $store.Open($OpenFlags)
    return $store
}

function Add-CertificateToCurrentUserStore(
    [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
    [string]$StoreName,
    [string]$Purpose
) {
    $store = $null
    try {
        $store = Open-CurrentUserStore $StoreName
        $alreadyThere = $store.Certificates | Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint }
        if ($alreadyThere) {
            Write-Host "   CurrentUser\$StoreName ja contem o certificado ($Purpose)." -ForegroundColor DarkGray
            return
        }

        Write-Host "   Gravando certificado em CurrentUser\$StoreName ($Purpose)..." -ForegroundColor Cyan
        $store.Add($Certificate)

        $installed = $store.Certificates | Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint }
        if (-not $installed) {
            throw "O certificado nao apareceu em CurrentUser\$StoreName apos a gravacao."
        }
    } finally {
        if ($store) { $store.Close() }
    }
}

Write-Host "==> Verificando certificado do usuario atual..." -ForegroundColor Cyan
$certificates = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
                Where-Object { $_.Subject -eq $CertSubject }
$cert = $certificates | Select-Object -First 1

if ($RecreateCertificate) {
    Write-Host "==> Removendo certificado(s) anterior(es) e chaves quebradas..." -ForegroundColor Yellow
    Write-Host "   Atencao: o certificado e compartilhado com o NXProject; rode sign-nxproject.ps1 depois para reassinar o NXProject." -ForegroundColor Yellow
    $thumbprints = @($certificates | ForEach-Object Thumbprint)

    foreach ($storeName in @("My", "TrustedPublisher", "Root")) {
        try {
            $store = Open-CurrentUserStore $storeName
            $toRemove = $store.Certificates | Where-Object { $_.Thumbprint -in $thumbprints -or $_.Subject -eq $CertSubject }
            foreach ($c in $toRemove) { $store.Remove($c) }
            $store.Close()
        } catch { }
    }

    $cert = $null
}

if (-not $cert) {
    Write-Host "==> Criando certificado local sem solicitar permissao de administrador..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $CertSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -FriendlyName "NXProject Dev Local" `
        -NotAfter (Get-Date).AddYears(10) `
        -ErrorAction Stop
    Write-Host "   Certificado criado: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    Write-Host "   Certificado ja existe: $($cert.Thumbprint)" -ForegroundColor Green
}

Write-Host "==> Verificando certificado nos stores confiaveis..." -ForegroundColor Cyan
try {
    Add-CertificateToCurrentUserStore $cert "Root" "raiz confiavel do usuario atual"
    Add-CertificateToCurrentUserStore $cert "TrustedPublisher" "publicador confiavel do usuario atual"
    Write-Host "   Certificado confiavel." -ForegroundColor Green
} catch {
    Write-Host "   Erro ao gravar certificado em store CurrentUser: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Tente recriar o certificado ou execute uma vez em PowerShell elevado:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -RecreateCertificate" -ForegroundColor Cyan
    exit 1
}

# Assina so os binarios PROPRIOS (NXPilot*). Os de terceiros (ONNX Runtime, LLamaSharp,
# runtime .NET) ja vem assinados pelos fabricantes — reassinar trocaria a assinatura deles.
Write-Host "==> Assinando binarios..." -ForegroundColor Cyan
$signed = 0
$failed = 0
foreach ($dir in $ProjectBin) {
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem $dir -Include "NxPilot*.exe","NxPilot*.dll","NXPilot*.exe","NXPilot*.dll" -Recurse | ForEach-Object {
        try {
            $result = Set-AuthenticodeSignature -FilePath $_.FullName -Certificate $cert -ErrorAction Stop
            if ($result.Status -eq "Valid") {
                $signed++
            }
            else {
                $failed++
                Write-Host "   Falha: $($_.Name) - $($result.Status): $($result.StatusMessage)" -ForegroundColor Red
            }
        }
        catch {
            $failed++
            Write-Host "   Falha: $($_.Name) - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}
Write-Host "   $signed arquivo(s) assinado(s), $failed falha(s)." -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "A assinatura nao foi concluida." -ForegroundColor Red
    if (-not $RecreateCertificate) {
        Write-Host "Tente novamente recriando o certificado:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -RecreateCertificate" -ForegroundColor Cyan
    }
    exit 1
}

Write-Host ""
Write-Host "Pronto! Execute o run-nxpilot.ps1 normalmente." -ForegroundColor Cyan
