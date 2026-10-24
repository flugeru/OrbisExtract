# Script to create Windows icon from PNG for OrbisExtract
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$iconSource = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Assets\Icon.png'
$outputDir = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Assets'

# Check if Icon.png exists
if (-not (Test-Path $iconSource)) {
    Write-Error "Source icon not found at: $iconSource"
    exit 1
}

# Use ImageMagick to convert PNG to ICO
Write-Host "Converting PNG to ICO..."
try {
    & magick convert -density 300 $iconSource $outputDir\Icon.ico
    Write-Host "Successfully created Icon.ico from PNG"
} catch {
    Write-Warning "ImageMagick not found or failed. Please install ImageMagick:"
    Write-Warning "  Windows: https://imagemagick.org/script/download.php#windows"
    Write-Warning "  Or use an online converter like: https://convertio.co/pt/png-ico/"
    exit 1
}

Write-Host "Output directory: $outputDir"
if (Test-Path (Join-Path $outputDir 'Icon.ico')) {
    Write-Host "Icon.ico created successfully!"
} else {
    Write-Warning "Icon.ico was not created. Please create it manually."
}
