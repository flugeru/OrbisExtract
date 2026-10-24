$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App.Desktop\OrbisExtract.App.Desktop.csproj'
$out = Join-Path $root 'artifacts\win-x64'

# Create icon from PNG if Icon.ico doesn't exist
if (-not (Test-Path (Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Assets\Icon.ico'))) {
    Write-Host "Creating Icon.ico from PNG..."
    try {
        & magick convert -density 300 `
            (Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Assets\Icon.png') `
            (Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Assets\Icon.ico')
        Write-Host "Successfully created Icon.ico"
    } catch {
        Write-Warning "ImageMagick not found. Skipping icon creation."
        Write-Warning "Please install ImageMagick and run the script manually to create Icon.ico"
    }
}

dotnet publish $project -c Release -r win-x64 --self-contained true -o $out
Write-Host "Publicado em $out"
