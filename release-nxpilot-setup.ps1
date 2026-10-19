<#
.SYNOPSIS
Gera o NXPilot-Setup.zip (a BASE: runtime + bibliotecas de terceiros + um app embutido) e,
com -Upload, publica na release FIXA "nxpilot-setup-latest" do GitHub.

Mesmo modelo do NXProject: o Setup NAO acompanha cada versao. Cada versao publica so o
NXPilot-Release.zip (o nucleo do app), que o Setup e o "Verificar atualizacao" do app baixam.
O Setup so e republicado quando a base muda (runtime/bibliotecas) ou o proprio Setup muda.
Ele continua levando um app embutido: sem internet, instala essa versao.

O carimbo de build (setup-build-timestamp.txt) vai dentro do Setup, e o Setup o grava na
pasta do app ao instalar. Na release fixa vai junto o NXPilot-Setup.timestamp.txt com o
mesmo valor: o app compara os dois para saber se ha um Setup novo (sem falso positivo).

  .\release-nxpilot-setup.ps1                 # gera dist\setup\NXPilot-Setup.zip
  .\release-nxpilot-setup.ps1 -Upload         # ...e publica na release fixa nxpilot-setup-latest
  .\release-nxpilot-setup.ps1 -Sign           # assina os binarios proprios antes de compactar

Versao nova do app (testes, versao, pacote, commit, tag, release): .\release-nxpilot-new-version.ps1
#>
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$Sign,
    [switch]$SkipTests,
    [switch]$Upload,
    [string]$SetupTag = "nxpilot-setup-latest"
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
# Mesma receita do release do NXProject contra o NETSDK1047: um build/restore anterior SEM
# RID (o build da solution) deixa o project.assets.json sem 'win-x64'. Encerra os build
# servers (seguram o obj/ e o assets.json velho sobrevive), apaga o obj/ e restaura com o RID.
# O publish fica SEM --no-restore de proposito: o projeto temporario do WPF (*_wpftmp) refaz
# o restore no meio e, com --no-restore, o publish leria o arquivo que ele mesmo sobrescreveu.
dotnet build-server shutdown 2>&1 | Out-Null
foreach ($proj in @($AppProject, $SetupProject)) {
    $objDir = Join-Path (Split-Path $proj) "obj"
    for ($attempt = 1; (Test-Path $objDir) -and $attempt -le 4; $attempt++) {
        try { Remove-Item $objDir -Recurse -Force -ErrorAction Stop }
        catch {
            if ($attempt -eq 4) { Write-Host "Nao foi possivel remover $objDir (pasta ocupada). Feche o app/IDE e tente de novo." -ForegroundColor Red; exit 1 }
            dotnet build-server shutdown 2>&1 | Out-Null
            Start-Sleep -Milliseconds 700
        }
    }
}
dotnet restore $AppProject -r $Runtime -p:SelfContained=true --nologo -v q --force
dotnet publish $AppProject -c $Configuration -r $Runtime --self-contained true -o $AppPublishDir --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha ao publicar NXPilot.Desktop." -ForegroundColor Red
    exit 1
}

Write-Step "Publicando NXPilot-Setup self-contained ($Runtime)..."
if (Test-Path $SetupPublishDir) { Remove-Item -LiteralPath $SetupPublishDir -Recurse -Force }
dotnet restore $SetupProject -r $Runtime -p:SelfContained=true --nologo -v q --force
dotnet publish $SetupProject -c $Configuration -r $Runtime --self-contained true -o $SetupPublishDir --nologo
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

# Carimbo de build da base: dentro do Setup (vai para a pasta do app) e ao lado do zip
# (vai para a release fixa). Mesmo valor nos dois lugares.
$stamp = [DateTime]::UtcNow.ToString("o")
Set-Content -Path (Join-Path $SetupPublishDir "setup-build-timestamp.txt") -Value $stamp -Encoding ascii -NoNewline
$StampFile = Join-Path $SolutionDir "dist\setup\NXPilot-Setup.timestamp.txt"
Set-Content -Path $StampFile -Value $stamp -Encoding ascii -NoNewline

# Lista da base (tudo que NAO e o nucleo do app, com tamanho): o release de versao compara com
# a ultima base publicada para saber se precisa republicar o Setup.
$CorePattern = '^(NxPilot|NXPilot\.(Core|Databricks|GitRepos|Mail|WindowsUi)|NXData\.AgentInstaller)\.(exe|dll|pdb|deps\.json|runtimeconfig\.json)$'
$BaseList = Join-Path $SolutionDir "dist\setup\base-files.txt"
Get-ChildItem -Path $AppPublishDir -Recurse -File |
    Where-Object { $_.Name -notmatch $CorePattern } |
    ForEach-Object { "{0}|{1}" -f $_.FullName.Substring($AppPublishDir.Length).TrimStart('\'), $_.Length } |
    Sort-Object | Set-Content -Path $BaseList -Encoding utf8

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

if ($Upload) {
    Write-Step "Publicando o Setup na release fixa $SetupTag..."
    gh release view $SetupTag --repo nexusxdata/NXPilot --json tagName 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        # Pre-release e nunca "Latest": a pagina de releases destaca a versao do app, nao a base.
        gh release create $SetupTag --repo nexusxdata/NXPilot --prerelease --latest=false `
            --title "NXPilot Setup (instalador)" `
            --notes "Instalador do NXPilot (base: runtime .NET e bibliotecas). Esta release e fixa e sempre traz o NXPilot-Setup.zip mais recente; ao instalar, ele baixa a versao mais nova do app. As versoes do app tem tags proprias (vX.Y.Z.WW)."
        if ($LASTEXITCODE -ne 0) { Write-Host "Falha ao criar a release $SetupTag." -ForegroundColor Red; exit 1 }
    }
    gh release upload $SetupTag $SetupOutputZip $StampFile --repo nexusxdata/NXPilot --clobber
    if ($LASTEXITCODE -ne 0) { Write-Host "Falha ao enviar o Setup para $SetupTag." -ForegroundColor Red; exit 1 }
    # A base publicada passa a ser esta: o release de versao so republica se ela mudar.
    Copy-Item $BaseList (Join-Path $SolutionDir "dist\setup\base-files.published.txt") -Force
    Write-Host "  https://github.com/nexusxdata/NXPilot/releases/tag/$SetupTag" -ForegroundColor DarkGray
}
