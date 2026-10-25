# Gera packaging\windows\orbisextract.ico a partir do PNG (sem ImageMagick).
# Usa System.Drawing e monta o ICO manualmente (entradas BMP 32bpp + 1 PNG 256px).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'packaging\linux\orbisextract.png'
if (-not (Test-Path $src)) {
    $src = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Assets\Icon.png'
}
if (-not (Test-Path $src)) {
    Write-Error "PNG de origem não encontrado: $src"
    exit 1
}
$outDir = Join-Path $root 'packaging\windows'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'orbisextract.ico'

function New-ResizedBitmap {
    param([System.Drawing.Image]$img, [int]$size)
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($img, 0, 0, $size, $size)
    $g.Dispose()
    return $bmp
}

function New-BmpEntry {
    param([System.Drawing.Bitmap]$bmp)
    $s = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    $andRowBytes = [int]([Math]::Ceiling($s / 32.0) * 4)
    $andSize = $andRowBytes * $s
    $xorSize = $s * $s * 4

    # BITMAPINFOHEADER (40 bytes), altura dobrada (XOR + AND)
    $bw.Write([int]40)
    $bw.Write([int]$s)
    $bw.Write([int]($s * 2))
    $bw.Write([int16]1)        # planes
    $bw.Write([int16]32)       # bit count
    $bw.Write([int]0)          # compression = BI_RGB
    $bw.Write([int]($xorSize + $andSize))
    $bw.Write([int]0)          # XPelsPerMeter
    $bw.Write([int]0)          # YPelsPerMeter
    $bw.Write([int]0)          # clrUsed
    $bw.Write([int]0)          # clrImportant

    # Pixels BGRA, de baixo para cima
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B)
            $bw.Write([byte]$c.G)
            $bw.Write([byte]$c.R)
            $bw.Write([byte]$c.A)
        }
    }
    # Máscara AND (tudo 0 — o canal alfa governa a transparência)
    $bw.Write((New-Object byte[] $andSize))
    $bw.Flush()
    return $ms.ToArray()
}

$img = [System.Drawing.Image]::FromFile((Resolve-Path $src).Path)
try {
    $entries = @()
    foreach ($s in @(16, 24, 32, 48, 64, 128)) {
        $bmp = New-ResizedBitmap $img $s
        $entries += , @{ Size = $s; Data = (New-BmpEntry $bmp) }
        $bmp.Dispose()
    }
    # 256px como PNG embutido (menor e suportado desde o Vista)
    $bmp256 = New-ResizedBitmap $img 256
    $msPng = New-Object System.IO.MemoryStream
    $bmp256.Save($msPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp256.Dispose()
    $entries += , @{ Size = 256; Data = $msPng.ToArray() }
    $msPng.Dispose()
}
finally {
    $img.Dispose()
}

# Monta o arquivo ICO
$fs = [System.IO.File]::Create($out)
try {
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([int16]0)                       # reserved
    $bw.Write([int16]1)                       # type = icon
    $bw.Write([int16]$entries.Count)
    $offset = 6 + (16 * $entries.Count)
    foreach ($e in $entries) {
        $dim = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
        $bw.Write([byte]$dim)                 # width
        $bw.Write([byte]$dim)                 # height
        $bw.Write([byte]0)                    # colors
        $bw.Write([byte]0)                    # reserved
        $bw.Write([int16]1)                   # planes
        $bw.Write([int16]32)                  # bpp
        $bw.Write([int]$e.Data.Length)
        $bw.Write([int]$offset)
        $offset += $e.Data.Length
    }
    foreach ($e in $entries) { $bw.Write([byte[]]$e.Data) }
    $bw.Flush()
}
finally {
    $fs.Dispose()
}

Write-Output "ICO gerado: $out ($((Get-Item $out).Length) bytes)"
