<#
.SYNOPSIS
    Trims leading and trailing silence from a 16-bit PCM WAV, with a short fade-out.

.DESCRIPTION
    Sound packs habitually ship an impact with a fraction of a second of silence in front of it.
    That is harmless in a sampler and fatal in a game: the effect is fired on the frame the blow
    connects, so 250 ms of leading silence is 15 frames of delay between the punch on screen and
    the sound, which reads as broken audio rather than as a late sound.

    The onset is found relative to the clip's own peak, so it does not matter how hot the file is
    mastered. A few milliseconds are kept in front of it (the transient IS the punch, and cutting
    into it dulls the hit), and a short fade closes the tail so the trim cannot click.

.PARAMETER InputPath
    The source .wav (16-bit PCM, any sample rate, mono or stereo).

.PARAMETER Output
    Where to write the trimmed .wav.

.EXAMPLE
    pwsh tools/trim-sound-silence.ps1 -InputPath Curitiba/Curitiba.Art/punch_1.wav `
        -Output Curitiba/Curitiba.Core/Content/Sounds/PunchHit1.wav
#>
param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$Output,
    # Fractions of the clip's own peak: ~-46 dB for the onset, ~-54 dB for the tail.
    [double]$OnsetThreshold = 0.005,
    [double]$TailThreshold = 0.002,
    [int]$PreRollMs = 5,
    [int]$PadMs = 20,
    [int]$FadeOutMs = 15
)

$ErrorActionPreference = 'Stop'

$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $InputPath))
if ([System.Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'RIFF' -or
    [System.Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne 'WAVE') {
    throw "$InputPath is not a RIFF/WAVE file."
}

$fmtPos = -1; $dataPos = -1; $dataSize = 0
$pos = 12
while ($pos -lt ($bytes.Length - 8)) {
    $id = [System.Text.Encoding]::ASCII.GetString($bytes, $pos, 4)
    $size = [BitConverter]::ToUInt32($bytes, $pos + 4)
    if ($id -eq 'fmt ') { $fmtPos = $pos + 8 }
    elseif ($id -eq 'data') { $dataPos = $pos + 8; $dataSize = $size }
    $pos += 8 + $size + ($size % 2)
}
if ($fmtPos -lt 0 -or $dataPos -lt 0) { throw "$InputPath has no fmt/data chunk." }

$audioFormat = [BitConverter]::ToUInt16($bytes, $fmtPos)
$channels = [BitConverter]::ToUInt16($bytes, $fmtPos + 2)
$sampleRate = [BitConverter]::ToUInt32($bytes, $fmtPos + 4)
$bits = [BitConverter]::ToUInt16($bytes, $fmtPos + 14)
if ($audioFormat -ne 1 -or $bits -ne 16) {
    throw "$InputPath is not 16-bit PCM (format=$audioFormat, bits=$bits)."
}

$bytesPerFrame = $channels * 2
$frames = [int]($dataSize / $bytesPerFrame)

# Per-frame peak across channels, so the envelope is read once.
$peaks = New-Object 'int[]' $frames
$clipPeak = 0
for ($f = 0; $f -lt $frames; $f++) {
    $m = 0
    $base = $dataPos + ($f * $bytesPerFrame)
    for ($c = 0; $c -lt $channels; $c++) {
        $a = [Math]::Abs([int][BitConverter]::ToInt16($bytes, $base + ($c * 2)))
        if ($a -gt $m) { $m = $a }
    }
    $peaks[$f] = $m
    if ($m -gt $clipPeak) { $clipPeak = $m }
}
if ($clipPeak -eq 0) { throw "$InputPath is silent all the way through." }

$onsetLevel = $clipPeak * $OnsetThreshold
$tailLevel = $clipPeak * $TailThreshold

$start = 0
while ($start -lt $frames -and $peaks[$start] -le $onsetLevel) { $start++ }
$end = $frames - 1
while ($end -gt $start -and $peaks[$end] -le $tailLevel) { $end-- }
if ($start -ge $frames) { throw "$InputPath never rises above the onset threshold." }

$start = [Math]::Max(0, $start - [int]($sampleRate * $PreRollMs / 1000))
$end = [Math]::Min($frames - 1, $end + [int]($sampleRate * $PadMs / 1000))
$keep = $end - $start + 1

# Copy the kept frames out, then fade the tail so the cut cannot click.
$outData = New-Object 'byte[]' ($keep * $bytesPerFrame)
[Array]::Copy($bytes, $dataPos + ($start * $bytesPerFrame), $outData, 0, $outData.Length)

$fadeFrames = [Math]::Min($keep, [int]($sampleRate * $FadeOutMs / 1000))
if ($fadeFrames -gt 0) {
    for ($i = 0; $i -lt $fadeFrames; $i++) {
        $gain = 1.0 - (($i + 1) / [double]$fadeFrames)
        $f = $keep - $fadeFrames + $i
        for ($c = 0; $c -lt $channels; $c++) {
            $o = ($f * $bytesPerFrame) + ($c * 2)
            $v = [int]([Math]::Round([BitConverter]::ToInt16($outData, $o) * $gain))
            [Array]::Copy([BitConverter]::GetBytes([int16]$v), 0, $outData, $o, 2)
        }
    }
}

$byteRate = $sampleRate * $bytesPerFrame
$out = New-Object 'byte[]' (44 + $outData.Length)
$w = New-Object System.IO.MemoryStream(, $out)
$bw = New-Object System.IO.BinaryWriter($w)
$bw.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
$bw.Write([uint32](36 + $outData.Length))
$bw.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
$bw.Write([uint32]16)
$bw.Write([uint16]1)
$bw.Write([uint16]$channels)
$bw.Write([uint32]$sampleRate)
$bw.Write([uint32]$byteRate)
$bw.Write([uint16]$bytesPerFrame)
$bw.Write([uint16]16)
$bw.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
$bw.Write([uint32]$outData.Length)
$bw.Write($outData)
$bw.Flush()

$dir = Split-Path -Parent $Output
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
[System.IO.File]::WriteAllBytes($Output, $out)

$before = $frames / [double]$sampleRate
$after = $keep / [double]$sampleRate
Write-Host ("{0} -> {1}  {2:N3}s -> {3:N3}s  (cut {4:N3}s of lead-in)" -f `
    (Split-Path -Leaf $InputPath), (Split-Path -Leaf $Output), $before, $after, ($start / [double]$sampleRate))
