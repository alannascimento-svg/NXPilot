<#
.SYNOPSIS
Gera uma NOVA VERSAO do NXPilot Community — mesmo fluxo do release-community-new-version.ps1
do NXProject: testa, sobe a versao, empacota, valida, commita, cria a tag e publica a release.

  .\release-nxpilot-new-version.ps1                 # bump de build (0.1.0.01 -> 0.1.0.02)
  .\release-nxpilot-new-version.ps1 -Bump minor     # 0.1.0.02 -> 0.2.0.01
  .\release-nxpilot-new-version.ps1 -Sign           # assina os binarios antes de compactar
  .\release-nxpilot-new-version.ps1 -NoPublish      # so local: commit + tag, sem push nem GitHub Release
  .\release-nxpilot-new-version.ps1 -WithSetup      # republica o Setup mesmo sem a base mudar

Modelo do NXProject: cada versao publica SO o NXPilot-Release.zip (nucleo do app, poucos MB),
que o Setup e o "Configurar -> Verificar atualizacao" do app baixam. O Setup (base: runtime e
bibliotecas) vive na release FIXA nxpilot-setup-latest e so e republicado quando a base muda.

Ordem (cada passo so roda se o anterior passou):
  1. build + smoke test + testes criticos (build-nxpilot.ps1) — falhou, nada muda
  2. versao nova em Directory.Build.props (vale para app, Setup e Bench)
  3. publica o app e monta o Setup local (release-nxpilot-setup.ps1); NXPilot-Release.zip com
     o nucleo + validacao; compara a base com a ultima publicada
  4. commit "Release vX" SO com arquivos ja rastreados (git add -u; nunca adiciona untracked)
  5. tag anotada vX no commit da versao
  6. com remoto 'origin': push do branch e da tag e GitHub Release com o NXPilot-Release.zip.
     A tag so e publicada se o commit ja estiver no remoto (o "Source code" da release tem
     que casar com os binarios — mesma regra do NXProject).
     Notas da release: release-notes.md (se existir; "{versao}" vira a versao nova).
  7. se a base mudou (ou -WithSetup): Setup + carimbo na release fixa nxpilot-setup-latest.
#>
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("build", "patch", "minor", "major")]
    [string]$Bump = "build",
    [switch]$Sign,
    [switch]$NoPublish,
    # Republica o Setup na release fixa mesmo sem a base mudar (ex.: mudou a tela do Setup).
    [switch]$WithSetup
)

$SolutionDir = $PSScriptRoot
$PropsFile = Join-Path $SolutionDir "Directory.Build.props"
$SetupZip = Join-Path $SolutionDir "dist\setup\NXPilot-Setup.zip"
$AppPublishDir = Join-Path $SolutionDir "dist\app\publish-win-x64"
$ReleaseDir = Join-Path $SolutionDir "dist\release"
$ReleaseZip = Join-Path $ReleaseDir "NXPilot-Release.zip"
$GitHubRepo = "nexusxdata/NXPilot"
$SetupTag = "nxpilot-setup-latest"
# Nucleo do app (vai em cada versao). O resto (runtime, ONNX, LLamaSharp, FlaUI...) e a BASE,
# que vem no Setup — mesma regra do release-nxpilot-setup.ps1.
$CorePattern = '^(NxPilot|NXPilot\.(Core|Databricks|GitRepos|Mail|WindowsUi)|NXData\.AgentInstaller)\.(exe|dll|deps\.json|runtimeconfig\.json)$'
$RequiredInRelease = @(
    "NxPilot.exe", "NxPilot.dll", "NxPilot.deps.json", "NxPilot.runtimeconfig.json", "NXPilot.Core.dll",
    "NXPilot.Databricks.dll", "NXPilot.GitRepos.dll", "NXPilot.Mail.dll", "NXData.AgentInstaller.dll", "NXPilot.WindowsUi.dll",
    "LICENSE.txt", "LICENSE.en.txt", "THIRD-PARTY-NOTICES.txt"
)

function Write-Step($msg) {
    Write-Host ""
    Write-Host ">> $msg" -ForegroundColor Cyan
}

function Fail($msg) {
    Write-Host ""
    Write-Host $msg -ForegroundColor Red
    exit 1
}

# ── Versao (mesmo formato do NXProject: major.minor.patch.build com build de 2 digitos) ──
function Get-Version {
    $content = Get-Content $PropsFile -Raw
    if ($content -match '<Version>([^<]+)</Version>') { return $Matches[1] }
    return "0.1.0.0"
}

function Step-Version([string]$Version, [string]$BumpType) {
    $p = $Version.Split('.')
    $major = [int]$p[0]
    $minor = if ($p.Count -gt 1) { [int]$p[1] } else { 0 }
    $patch = if ($p.Count -gt 2) { [int]$p[2] } else { 0 }
    $build = if ($p.Count -gt 3) { [int]$p[3] } else { 0 }
    switch ($BumpType) {
        "major" { $major++; $minor = 0; $patch = 0; $build = 1 }
        "minor" { $minor++; $patch = 0; $build = 1 }
        "patch" { $patch++; $build = 1 }
        "build" { $build++; if ($build -gt 99) { $patch++; $build = 1 } }
    }
    return "{0}.{1}.{2}.{3:00}" -f $major, $minor, $patch, $build
}

function Set-Version([string]$NewVersion) {
    $p = $NewVersion.Split('.')
    $assembly = "{0}.{1}.{2}.{3}" -f [int]$p[0], [int]$p[1], [int]$p[2], [int]$p[3]
    $content = Get-Content $PropsFile -Raw
    $content = $content -replace '<Version>[^<]+</Version>', "<Version>$NewVersion</Version>"
    $content = $content -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$assembly</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$assembly</FileVersion>"
    $content = $content -replace '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$NewVersion</InformationalVersion>"
    Set-Content -Path $PropsFile -Value $content -Encoding UTF8 -NoNewline
}

# ── Pre-condicoes de git: decide ANTES de mexer em qualquer arquivo ──
$IsGit = $false
git -C $SolutionDir rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -eq 0) {
    $IsGit = $true
    git -C $SolutionDir rev-parse --verify HEAD *> $null
    if ($LASTEXITCODE -ne 0) {
        Fail "O repositorio ainda nao tem commit. Faca o commit inicial (sem 'git add -A') e rode de novo."
    }
    $dirty = git -C $SolutionDir status --porcelain --untracked-files=no
    if ($dirty) {
        Write-Host "Ha alteracoes rastreadas nao commitadas — elas entrariam no commit da versao:" -ForegroundColor Yellow
        $dirty | ForEach-Object { Write-Host "   $_" }
        Fail "Commite ou descarte essas alteracoes antes de gerar a versao."
    }
}
else {
    Write-Host "Aviso: nao e um repositorio git — a versao sera gerada sem commit/tag." -ForegroundColor Yellow
}

$CurrentVersion = Get-Version
$NewVersion = Step-Version $CurrentVersion $Bump
$Tag = "v$NewVersion"
if ($IsGit -and (git -C $SolutionDir tag --list $Tag)) { Fail "A tag $Tag ja existe." }

# ── 1. Testes ANTES de mudar a versao: se falhar, nada muda ──
Write-Step "Build + smoke test + testes criticos..."
& (Join-Path $SolutionDir "build-nxpilot.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { Fail "Testes falharam. Versao NAO gerada (nada foi alterado)." }

# ── 2. Versao ──
Write-Host ""
Write-Host ">> Versao: $CurrentVersion -> $NewVersion" -ForegroundColor Yellow
Set-Version $NewVersion

# ── 3. Pacotes ──
# O script do Setup publica o app (dist\app) e monta o Setup local (base + app embutido).
# O Setup so SOBE para o GitHub se a base mudou (ou -WithSetup) — passo 6.
Write-Step "Publicando o app e montando o Setup local $Tag..."
$setupArgs = @{ Configuration = $Configuration; SkipTests = $true }
if ($Sign) { $setupArgs.Sign = $true }
& (Join-Path $SolutionDir "release-nxpilot-setup.ps1") @setupArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $SetupZip)) {
    Set-Version $CurrentVersion
    Fail "Falha ao publicar o app/Setup. Versao revertida para $CurrentVersion."
}

Write-Step "Gerando NXPilot-Release.zip (nucleo do app)..."
$stage = Join-Path $ReleaseDir "stage"
if (Test-Path $ReleaseDir) { Remove-Item -LiteralPath $ReleaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Get-ChildItem -Path $AppPublishDir -File | Where-Object { $_.Name -match $CorePattern } | Copy-Item -Destination $stage
foreach ($doc in @("LICENSE.txt", "LICENSE.en.txt", "THIRD-PARTY-NOTICES.txt")) { Copy-Item (Join-Path $SolutionDir $doc) $stage }
$missing = $RequiredInRelease | Where-Object { -not (Test-Path (Join-Path $stage $_)) }
if ($missing) {
    Set-Version $CurrentVersion
    Fail "Arquivos ausentes no NXPilot-Release.zip: $($missing -join ', '). Versao revertida para $CurrentVersion."
}
$exeVersion = (Get-Item (Join-Path $stage "NxPilot.exe")).VersionInfo.ProductVersion
if ($exeVersion -notlike "$NewVersion*") {
    Set-Version $CurrentVersion
    Fail "NxPilot.exe saiu com a versao '$exeVersion' (esperado $NewVersion). Versao revertida."
}
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $ReleaseZip -Force
$sizeMb = [Math]::Round((Get-Item $ReleaseZip).Length / 1MB, 1)
Write-Host "  OK: $((Get-ChildItem $stage).Count) arquivos, NxPilot.exe $exeVersion, $sizeMb MB." -ForegroundColor DarkGray

# A base mudou desde o ultimo Setup publicado? (lista de arquivos de terceiros + tamanho)
$BaseNow = Join-Path $SolutionDir "dist\setup\base-files.txt"
$BasePublished = Join-Path $SolutionDir "dist\setup\base-files.published.txt"
$NeedSetup = $WithSetup -or -not (Test-Path $BasePublished) -or
             ((Get-Content $BaseNow -Raw) -ne (Get-Content $BasePublished -Raw))
Write-Host ("  Setup (base): " + $(if ($NeedSetup) { "sera republicado na release $SetupTag." } else { "sem mudanca — nao sera republicado." })) -ForegroundColor DarkGray

if (-not $IsGit) {
    Write-Host ""
    Write-Host "Versao $NewVersion gerada (sem git): $ReleaseZip" -ForegroundColor Green
    exit 0
}

# ── 4/5. Commit e tag ──
Write-Step "Commit e tag $Tag..."
git -C $SolutionDir add -u   # so arquivos JA rastreados (Directory.Build.props); dist/ fica fora
git -C $SolutionDir diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git -C $SolutionDir diff --cached --name-only | ForEach-Object { Write-Host "     $_" }
    git -C $SolutionDir commit -q -m "Release $Tag"
    if ($LASTEXITCODE -ne 0) { Fail "Falha no commit da versao." }
}
git -C $SolutionDir tag -a $Tag -m "NXPilot Community $Tag"
if ($LASTEXITCODE -ne 0) { Fail "Falha ao criar a tag $Tag." }
$ReleaseCommit = (git -C $SolutionDir rev-parse HEAD).Trim()
Write-Host "  Commit $($ReleaseCommit.Substring(0, 8)) com a tag $Tag." -ForegroundColor Green

# ── 6. Publicacao ──
$origin = git -C $SolutionDir remote get-url origin 2>$null
$global:LASTEXITCODE = 0
if ($NoPublish -or -not $origin) {
    Write-Host ""
    Write-Host "Versao $Tag gerada localmente." -ForegroundColor Green
    Write-Host "  Pacote: $ReleaseZip ($sizeMb MB)" -ForegroundColor DarkGray
    if (-not $origin) {
        Write-Host "  Sem remoto 'origin': nada foi enviado. Para publicar depois:" -ForegroundColor Yellow
        Write-Host "    gh repo create $GitHubRepo --public --source . --remote origin" -ForegroundColor DarkGray
        Write-Host "    git push -u origin HEAD; git push origin $Tag" -ForegroundColor DarkGray
        Write-Host "    gh release create $Tag '$ReleaseZip' --verify-tag --title 'NXPilot Community $Tag' --repo $GitHubRepo" -ForegroundColor DarkGray
    }
    exit 0
}

Write-Step "Push do branch e da tag..."
$branch = (git -C $SolutionDir rev-parse --abbrev-ref HEAD).Trim()
git -C $SolutionDir push origin $branch
git -C $SolutionDir push origin $Tag
$remoteHead = (git -C $SolutionDir rev-parse "origin/$branch" 2>$null)
if (-not $remoteHead -or $remoteHead.Trim() -ne $ReleaseCommit) {
    Fail "O commit da versao nao chegou ao remoto; a release NAO foi publicada. Rode o push e depois: gh release create $Tag '$ReleaseZip' --verify-tag --repo $GitHubRepo"
}

Write-Step "Publicando GitHub Release $Tag..."
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Fail "gh CLI nao encontrado. Instale (https://cli.github.com), faca 'gh auth login' e rode: gh release create $Tag '$ReleaseZip' --verify-tag --repo $GitHubRepo"
}
$NotesSource = Join-Path $SolutionDir "release-notes.md"
$Notes = if (Test-Path $NotesSource) { (Get-Content $NotesSource -Raw -Encoding UTF8).Replace("{versao}", $Tag) }
         else { "NXPilot Community $Tag — Inteligencia Artificial Automatica, e nao autonoma: voce no controle." }
$NotesFile = Join-Path ([System.IO.Path]::GetTempPath()) "nxpilot-notes-$Tag.md"
[System.IO.File]::WriteAllText($NotesFile, $Notes, (New-Object System.Text.UTF8Encoding $false))
gh release create $Tag $ReleaseZip --verify-tag --title "NXPilot Community $Tag" --notes-file $NotesFile --repo $GitHubRepo
if ($LASTEXITCODE -ne 0) { Fail "Falha ao criar a release. Verifique: gh auth status" }

Write-Host ""
Write-Host "Release $Tag publicada (NXPilot-Release.zip, $sizeMb MB)!" -ForegroundColor Green
Write-Host "  https://github.com/$GitHubRepo/releases/tag/$Tag" -ForegroundColor DarkGray

# ── 7. Setup (base) na release fixa — so quando mudou ──
if ($NeedSetup) {
    Write-Step "Republicando o Setup na release fixa $SetupTag..."
    # Sobe o Setup ja montado neste release (mesma base e app embutido desta versao).
    gh release view $SetupTag --repo $GitHubRepo --json tagName 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        gh release create $SetupTag --repo $GitHubRepo --prerelease --latest=false --title "NXPilot Setup (instalador)" `
            --notes "Instalador do NXPilot (base: runtime .NET e bibliotecas). Esta release e fixa e sempre traz o NXPilot-Setup.zip mais recente; ao instalar, ele baixa a versao mais nova do app. As versoes do app tem tags proprias (vX.Y.Z.WW)."
        if ($LASTEXITCODE -ne 0) { Fail "Falha ao criar a release $SetupTag (a versao $Tag ja foi publicada)." }
    }
    gh release upload $SetupTag $SetupZip (Join-Path $SolutionDir "dist\setup\NXPilot-Setup.timestamp.txt") --repo $GitHubRepo --clobber
    if ($LASTEXITCODE -ne 0) { Fail "Falha ao enviar o Setup (a versao $Tag ja foi publicada). Rode: .\release-nxpilot-setup.ps1 -Upload" }
    Copy-Item $BaseNow $BasePublished -Force
    Write-Host "  Setup publicado: https://github.com/$GitHubRepo/releases/tag/$SetupTag" -ForegroundColor DarkGray
}
$global:LASTEXITCODE = 0
