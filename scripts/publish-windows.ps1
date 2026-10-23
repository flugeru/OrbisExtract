$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App.Desktop\OrbisExtract.App.Desktop.csproj'
$out = Join-Path $root 'artifacts\win-x64'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $out
Write-Host "Publicado em $out"
