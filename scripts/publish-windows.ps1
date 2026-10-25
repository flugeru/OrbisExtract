$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App.Desktop\OrbisExtract.App.Desktop.csproj'
$out = Join-Path $root 'artifacts\windows'

# Ícone do instalador/exe (sem ImageMagick — gera direto o .ico)
$icoScript = Join-Path $PSScriptRoot 'create-windows-icon.ps1'
if (-not (Test-Path (Join-Path $root 'packaging\windows\orbisextract.ico'))) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $icoScript
}

dotnet publish $project -c Release -r win-x64 --self-contained true -o $out
Write-Host "Publicado em $out"
