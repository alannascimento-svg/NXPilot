<#
.SYNOPSIS
Gera o NXPilot-Setup.zip: instalador self-contained que ja carrega o app inteiro.

Diferente do NXProject (Setup com libs de terceiros + nucleo baixado do GitHub), no
NXPilot o app vai DENTRO do Setup: uma release = um NXPilot-Setup.zip. Os dois
sao publicados self-contained com o mesmo SDK, entao compartilham o mesmo runtime solto
na pasta — o app nao duplica o runtime. O Setup instala copiando a pasta (menos os
arquivos dele mesmo) para %USERPROFILE%\Apps\NXPilot.

  .\release-nxpilot-setup.ps1           # gera dist\setup\NXPilot-Setup.zip
  .\release-nxpilot-setup.ps1 -Sign     # assina os binarios proprios antes de compactar

Versao nova (testes, versao, Setup, commit, tag, release): .\release-nxpilot-new-version.ps1
#>
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$Sign,
    [switch]$SkipTests
)

$SolutionDir = $PSScriptRoot
$AppProject = Join-Path $SolutionDir "NXPilot.Desktop\NXPilot.Desktop.csproj"
$SetupProject = Join-Path $SolutionDir "NXPilot-Setup\NXPilot-Setup.csproj"
$Runtime = "win-x64"
$AppPublishDir = Join-Path $SolutionDir "dist\app\publish-$Runtime"
$SetupPublishDir = Join-Path $SolutionDir "dist\setup\publish-$Runtime"
$SetupOutputZip = Join-Path $SolutionDir "dist\setup\NXPilot-Setup.zip"

# Mesmo conjunto que o Setup confere apos instalar (MainWindow.RequiredFiles): sem eles o
# Windows abre o dialogo generico "instale o .NET" em vez do app.
$RequiredFiles = @(
    "NXPilot-Setup.exe",
    "NxPilot.exe", "NxPilot.dll", "NXPilot.Core.dll", "NxPilot.runtimeconfig.json",
    "hostfxr.dll", "hostpolicy.dll", "coreclr.dll", "PresentationFramework.dll", "onnxruntime.dll"
)

function Write-Step($msg) {
    Write-Host ""
    Write-Host ">> $msg" -ForegroundColor Cyan
}

Get-Process -Name "NxPilot", "NXPilot", "NXPilot-Setup" -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $SkipTests) {
    Write-Step "Build + smoke test antes de empacotar..."
    & (Join-Path $SolutionDir "build-nxpilot.ps1") -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build/smoke test falhou. Setup NAO gerado." -ForegroundColor Red
        exit 1
    }
}

Write-Step "Publicando NXPilot self-contained ($Runtime)..."
if (Test-Path $AppPublishDir) { Remove-Item -LiteralPath $AppPublishDir -Recurse -Force }
# Restore explicito com o RID antes do publish: o build da solution (sem RID) deixa o
# project.assets.json sem 'net10.0-windows/win-x64' e o projeto temporario do WPF (_wpftmp)
# falha com NETSDK1047 se o restore acontecer dentro do proprio publish.
dotnet restore $AppProject -r $Runtime --nologo -v q
dotnet publish $AppProject -c $Configuration -r $Runtime --self-contained true --no-restore -o $AppPublishDir --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha ao publicar NXPilot.Desktop." -ForegroundColor Red
    exit 1
}

Write-Step "Publicando NXPilot-Setup self-contained ($Runtime)..."
if (Test-Path $SetupPublishDir) { Remove-Item -LiteralPath $SetupPublishDir -Recurse -Force }
dotnet restore $SetupProject -r $Runtime --nologo -v q
dotnet publish $SetupProject -c $Configuration -r $Runtime --self-contained true --no-restore -o $SetupPublishDir --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha ao publicar NXPilot-Setup." -ForegroundColor Red
    exit 1
}

Write-Step "Juntando o app na pasta do Setup (runtime compartilhado)..."
$appFiles = Get-ChildItem -Path $AppPublishDir -Recurse -File
foreach ($f in $appFiles) {
    $rel = $f.FullName.Substring($AppPublishDir.Length).TrimStart('\', '/')
    $dest = Join-Path $SetupPublishDir $rel
    $destFolder = Split-Path $dest -Parent
    if (-not (Test-Path $destFolder)) { New-Item -ItemType Directory -Path $destFolder -Force | Out-Null }
    Copy-Item -Path $f.FullName -Destination $dest -Force
}
Write-Host "  Arquivos do app: $($appFiles.Count)" -ForegroundColor DarkGray

# Licenca do NXPilot (clausula 3: o aviso acompanha toda copia distribuida) e avisos de
# terceiros (MIT, Apache etc.). O Setup copia a pasta para a instalacao, entao vao juntos.
foreach ($doc in @("LICENSE.txt", "LICENSE.en.txt", "THIRD-PARTY-NOTICES.txt")) {
    $src = Join-Path $SolutionDir $doc
    if (-not (Test-Path $src)) {
        Write-Host "Arquivo obrigatorio ausente: $doc. Setup NAO gerado." -ForegroundColor Red
        exit 1
    }
    Copy-Item -Path $src -Destination (Join-Path $SetupPublishDir $doc) -Force
}
Write-Host "  Licenca e avisos incluidos: LICENSE.txt, LICENSE.en.txt, THIRD-PARTY-NOTICES.txt" -ForegroundColor DarkGray

Write-Step "Validando conteudo do pacote..."
$missing = $RequiredFiles | Where-Object { -not (Test-Path (Join-Path $SetupPublishDir $_)) }
if ($missing) {
    Write-Host "Arquivos essenciais ausentes: $($missing -join ', ')" -ForegroundColor Red
    Write-Host "NXPilot-Setup.zip NAO deve ser publicado." -ForegroundColor Red
    exit 1
}
Write-Host "  Arquivos essenciais presentes ($($RequiredFiles.Count))." -ForegroundColor DarkGray

if ($Sign) {
    Write-Step "Assinando binarios proprios..."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $SolutionDir "sign-nxpilot.ps1")
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Assinatura falhou. Setup NAO gerado." -ForegroundColor Red
        exit 1
    }
}

Write-Step "Compactando NXPilot-Setup.zip..."
if (Test-Path $SetupOutputZip) { Remove-Item -LiteralPath $SetupOutputZip -Force }
Compress-Archive -Path (Join-Path $SetupPublishDir "*") -DestinationPath $SetupOutputZip -Force

$version = (Get-Item (Join-Path $SetupPublishDir "NxPilot.exe")).VersionInfo.FileVersion
$sizeMb = [Math]::Round((Get-Item $SetupOutputZip).Length / 1MB, 1)
Write-Host ""
Write-Host "NXPilot-Setup.zip gerado com sucesso! (NXPilot $version)" -ForegroundColor Green
Write-Host "  $SetupOutputZip ($sizeMb MB)" -ForegroundColor DarkGray
Write-Host "  Para testar sem zipar: .\run-nxpilot-setup.ps1" -ForegroundColor DarkGray
Write-Host "  Versao nova com commit, tag e GitHub Release: .\release-nxpilot-new-version.ps1" -ForegroundColor DarkGray
