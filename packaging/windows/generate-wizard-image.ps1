# Creates the branded Inno Setup side illustration from the repository's
# existing OrbisExtract icon. Only runs on the Windows GitHub Actions runner.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = Join-Path $root 'OrbisExtract-transparent.png'
$destination = Join-Path $PSScriptRoot 'wizard-brand.png'

$canvas = New-Object System.Drawing.Bitmap(480, 920)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$icon = [System.Drawing.Image]::FromFile($source)
try {
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#252525'))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    # Center the square logo inside the vertical side panel without stretching it.
    $graphics.DrawImage($icon, (New-Object System.Drawing.Rectangle(95, 310, 290, 290)))
    $canvas.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $icon.Dispose()
    $graphics.Dispose()
    $canvas.Dispose()
}
Write-Host "Created $destination"
