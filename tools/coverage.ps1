<#
.SYNOPSIS
    Roda a suite com cobertura e gera o mesmo relatorio HTML que o CI publica.

.DESCRIPTION
    Faz, na sua maquina, exatamente o que o job "Build and test" faz: coleta cobertura com
    coverlet.runsettings e gera o relatorio com o ReportGenerator — a ferramenta e local, fixada
    em .config/dotnet-tools.json na raiz do repo, entao CI e maquina de dev rodam a mesma versao.

    A limpeza dos resultados anteriores nao e higiene, e correcao: --results-directory cria uma
    subpasta GUID por execucao e o ReportGenerator SOMA todas as que encontrar, entao sem limpar o
    relatorio mistura o run de agora com os de ontem.

    As exclusoes nao sao repetidas aqui: elas vivem so em coverlet.runsettings, para o relatorio
    dizer a mesma coisa que o coverage.cobertura.xml que sobe como artifact.

.PARAMETER TestTarget
    Solucao ou projeto a testar. O padrao e o que o CI roda.

.PARAMETER Configuration
    Configuracao de build. O padrao e Release, para bater com o CI.

.PARAMETER ResultsDir
    Onde o dotnet test deposita os dados brutos de cobertura.

.PARAMETER OutputDir
    Onde o relatorio e gerado.

.PARAMETER ReportOnly
    Nao roda os testes; so reprocessa os dados ja coletados.

.PARAMETER NoBrowser
    Nao abre o navegador no fim (CI, sessao headless, WSL).

.EXAMPLE
    pwsh tools/coverage.ps1

.EXAMPLE
    # Loop rapido: so o projeto de testes, sem rebuildar a cabeca DesktopGL nem o conteudo
    pwsh tools/coverage.ps1 -TestTarget Curitiba/Curitiba.Tests/Curitiba.Tests.csproj -NoBrowser
#>
param(
    [string]$TestTarget = "Curitiba.CI.slnx",
    [string]$Configuration = "Release",
    [string]$ResultsDir = "artifacts/test-results",
    [string]$OutputDir = "artifacts/coverage",
    [switch]$ReportOnly,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

# Tudo e resolvido a partir da raiz do repo, para o script rodar de qualquer pasta.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$results = Join-Path $repoRoot $ResultsDir
$output = Join-Path $repoRoot $OutputDir

Push-Location $repoRoot
try {
    if (-not $ReportOnly) {
        if (Test-Path -LiteralPath $results) {
            Remove-Item -LiteralPath $results -Recurse -Force
        }

        dotnet test $TestTarget `
            --configuration $Configuration `
            --collect:"XPlat Code Coverage" `
            --settings coverlet.runsettings `
            --results-directory $results

        if ($LASTEXITCODE -ne 0) {
            # Cobertura e diagnostico: com teste vermelho o relatorio ainda vale, desde que voce
            # saiba que ele cobre so o que chegou a rodar.
            Write-Warning "Testes falharam (exit $LASTEXITCODE). O relatorio abaixo cobre apenas o que executou."
        }
    }

    $reports = @(Get-ChildItem -LiteralPath $results -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue)
    if ($reports.Count -eq 0) {
        throw "Nenhum coverage.cobertura.xml em '$results'. Rode o script sem -ReportOnly."
    }
    if ($reports.Count -gt 1) {
        Write-Warning "$($reports.Count) arquivos de cobertura encontrados; o relatorio vai soma-los."
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore falhou (exit $LASTEXITCODE)." }

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    # Os mesmos argumentos do passo "Coverage report" do ci.yml.
    $rgArgs = @(
        "-reports:$(Join-Path (Join-Path $results '**') 'coverage.cobertura.xml')"
        "-targetdir:$output"
        "-reporttypes:Html;MarkdownSummaryGithub;TextSummary"
        "-assemblyfilters:+Curitiba.Core"
        "-title:Curitiba"
        "-verbosity:Warning"
    )
    dotnet reportgenerator @rgArgs
    if ($LASTEXITCODE -ne 0) { throw "ReportGenerator falhou (exit $LASTEXITCODE)." }

    Get-Content -LiteralPath (Join-Path $output "Summary.txt") | Write-Host

    $index = Join-Path $output "index.html"
    Write-Host ""
    Write-Host "Relatorio: $index"

    if (-not $NoBrowser) {
        try {
            Invoke-Item -LiteralPath $index
        }
        catch {
            Write-Warning "Nao consegui abrir o navegador: $($_.Exception.Message). Abra '$index' na mao."
        }
    }
}
finally {
    Pop-Location
}
