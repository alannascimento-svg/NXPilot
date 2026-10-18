<#
.SYNOPSIS
Compila a solution do NXPilot (Release por padrao) e roda o smoke test do classificador.

O smoke test (NXPilot.Bench --smoke) mede Regras e Regras + ONNX nas 26 acoes de
referencia e falha o build se a acuracia cair abaixo do patamar medido. Sem o modelo
ONNX em C:\IA_LOCAL\ONNX ele testa so as regras (nao baixa nada).
Depois roda os testes criticos (NXPilot.Bench --test, fachada NXPilot.Testing).

  .\build-nxpilot.ps1                 # Release + smoke test
  .\build-nxpilot.ps1 -Run            # compila e abre o app
  .\build-nxpilot.ps1 -Clean -Sign    # limpo + assina com o certificado local
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Run,
    [switch]$Clean,
    [switch]$Sign,
    [switch]$SkipTests
)

$SolutionDir = $PSScriptRoot
$SolutionFile = Join-Path $SolutionDir "NXPilot.slnx"
$OutputDir = Join-Path $SolutionDir "NXPilot.Desktop\bin\$Configuration\net10.0-windows"
$BenchExe = Join-Path $SolutionDir "NXPilot.Bench\bin\$Configuration\net10.0\NXPilot.Bench.exe"

$SharedDllLockPattern = "because it is being used by another process"

function Write-Step($msg) {
    Write-Host ""
    Write-Host ">> $msg" -ForegroundColor Cyan
}

# O exe aberto trava as DLLs e o build falha com MSB3027; encerra antes de compilar.
function Stop-NXPilotProcess {
    $processes = Get-Process -Name "NxPilot", "NXPilot", "NXPilot-Setup" -ErrorAction SilentlyContinue
    if ($null -eq $processes) { return }

    Write-Step "Encerrando NXPilot em execucao..."
    $processes | Stop-Process -Force
    $processes | ForEach-Object { $_.WaitForExit(5000) | Out-Null }
}

function Invoke-DotnetCommandWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ActionLabel,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    $attempt = 1
    while ($attempt -le 2) {
        $output = & $Command 2>&1
        $exitCode = $LASTEXITCODE
        if ($output) {
            $output | ForEach-Object { Write-Host $_ }
        }

        if ($exitCode -eq 0) {
            return
        }

        $combinedOutput = ($output | Out-String)
        $hasDllLock = $combinedOutput -match [regex]::Escape($SharedDllLockPattern)
        if (-not $hasDllLock -or $attempt -eq 2) {
            Write-Host "$ActionLabel falhou." -ForegroundColor Red
            exit 1
        }

        Write-Step "Detectado bloqueio temporario de DLL do .NET. Reiniciando build server e tentando novamente..."
        dotnet build-server shutdown | Out-Host
        Start-Sleep -Seconds 1
        $attempt++
    }
}

# Remove arquivos temporarios _wpftmp.csproj gerados pelo C# Dev Kit do VS Code.
Get-ChildItem -Path $SolutionDir -Filter "*_wpftmp.csproj" -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

Stop-NXPilotProcess

if ($Clean) {
    Write-Step "Limpando build anterior..."
    Invoke-DotnetCommandWithRetry -ActionLabel "O clean" -Command {
        dotnet clean $SolutionFile -c $Configuration --nologo -v q
    }
}

Write-Step "Restaurando pacotes..."
Invoke-DotnetCommandWithRetry -ActionLabel "O restore" -Command {
    dotnet restore $SolutionFile --nologo -v q
}

Write-Step "Compilando NXPilot ($Configuration)..."
Invoke-DotnetCommandWithRetry -ActionLabel "A compilacao" -Command {
    dotnet build $SolutionFile -c $Configuration --nologo --no-restore
}

if ($SkipTests) {
    Write-Host "Smoke test ignorado (-SkipTests)." -ForegroundColor DarkGray
}
else {
    Write-Step "Smoke test do classificador de risco (Regras + ONNX)..."
    # Chama o exe direto: o dotnet run nao repassa argumentos de forma confiavel no PowerShell.
    Invoke-DotnetCommandWithRetry -ActionLabel "O smoke test" -Command {
        & $BenchExe --smoke
    }

    Write-Step "Testes criticos (gate, regras, hook, contratos, worktree, segredos)..."
    Invoke-DotnetCommandWithRetry -ActionLabel "Os testes criticos" -Command {
        & $BenchExe --test
    }
}

Write-Host ""
Write-Host "Build NXPilot concluido com sucesso!" -ForegroundColor Green
Write-Host "  Saida: $OutputDir" -ForegroundColor DarkGray

if ($Sign) {
    Write-Step "Assinando binarios..."
    $signScript = Join-Path $SolutionDir "sign-nxpilot.ps1"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $signScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Aviso: assinatura falhou (codigo $LASTEXITCODE). run-nxpilot.ps1 tentara novamente ao iniciar." -ForegroundColor Yellow
    }
}
else {
    Write-Host "Assinatura ignorada. Use -Sign para assinar os binarios com o certificado local." -ForegroundColor DarkGray
}

if ($Run) {
    Write-Step "Iniciando aplicacao..."
    $runScript = Join-Path $SolutionDir "run-nxpilot.ps1"
    . $runScript -Configuration $Configuration
}
