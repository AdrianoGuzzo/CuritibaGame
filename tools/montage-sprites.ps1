<#
.SYNOPSIS
    Monta uma pasta de quadros (frames) numa única tira horizontal de sprite,
    no formato que o engine do Curitiba espera (quadros quadrados, lado a lado).

.DESCRIPTION
    Lê todos os PNG de -InputDir em ordem alfabética (ex.: quadro0000.png ...),
    valida que são quadrados e do mesmo tamanho, e os concatena horizontalmente
    num PNG ARGB de 32 bits, preservando a transparência.

    O engine deriva o tamanho do quadro pela ALTURA da tira e a quantidade por
    Largura/Altura, entao a saida fica: (lado * nº de quadros) x lado.

.EXAMPLE
    pwsh tools/montage-sprites.ps1 `
      -InputDir "Curitiba/Curitiba.Core/Content/Sprites/Sofia/Idle" `
      -Output   "Curitiba/Curitiba.Core/Content/Sprites/Sofia/Idle.png"
#>
param(
    [Parameter(Mandatory = $true)] [string] $InputDir,
    [Parameter(Mandatory = $true)] [string] $Output,
    [string] $Filter = "*.png"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $InputDir)) {
    throw "Pasta de entrada nao encontrada: $InputDir"
}

$files = Get-ChildItem -LiteralPath $InputDir -Filter $Filter -File | Sort-Object Name
if ($files.Count -eq 0) {
    throw "Nenhum arquivo '$Filter' encontrado em: $InputDir"
}

# Carrega o primeiro quadro para definir o tamanho de referencia.
$first = [System.Drawing.Image]::FromFile($files[0].FullName)
$frameW = $first.Width
$frameH = $first.Height
$first.Dispose()

if ($frameW -ne $frameH) {
    throw "Os quadros precisam ser quadrados. '$($files[0].Name)' e $frameW x $frameH."
}

$count = $files.Count
$stripW = $frameW * $count
$stripH = $frameH

$strip = New-Object System.Drawing.Bitmap($stripW, $stripH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($strip)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

$x = 0
foreach ($file in $files) {
    $img = [System.Drawing.Image]::FromFile($file.FullName)
    try {
        if ($img.Width -ne $frameW -or $img.Height -ne $frameH) {
            throw "Quadro '$($file.Name)' e $($img.Width)x$($img.Height); esperado $frameW x $frameH."
        }
        $g.DrawImage($img, $x, 0, $frameW, $frameH)
    }
    finally {
        $img.Dispose()
    }
    $x += $frameW
}

$g.Dispose()

$outDir = Split-Path -Parent $Output
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$strip.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
$strip.Dispose()

Write-Host "OK: $count quadros -> $Output  ($stripW x $stripH, quadro $frameW x $frameH)"
