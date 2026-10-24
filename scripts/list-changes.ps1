# List modified files for OrbisExtract changes
$root = Split-Path -Parent $PSScriptRoot

Write-Host "=== ARQUIVOS MODIFICADOS ==="
Write-Host ""

$files = @(
    "src\OrbisExtract.App\OrbisExtract.App.Android\OrbisExtract.App.Android.csproj",
    "src\OrbisExtract.App\OrbisExtract.App.Android\Properties\AndroidManifest.xml",
    "src\OrbisExtract.App\OrbisExtract.App.Desktop\app.manifest",
    "src\OrbisExtract.App\OrbisExtract.App.Desktop\OrbisExtract.App.Desktop.csproj",
    "README.md",
    "scripts\publish-windows.ps1",
    "scripts\create-windows-icon.ps1"
)

$backups = @(
    "src\OrbisExtract.App\OrbisExtract.App\App.axaml.cs.backup",
    "src\OrbisExtract.App\OrbisExtract.App.Desktop\app.manifest.backup",
    "src\OrbisExtract.App\OrbisExtract.App.Android\OrbisExtract.App.Android.csproj.backup"
)

$created = @(
    "src\OrbisExtract.App\OrbisExtract.App\Assets"
)

foreach ($file in $files + $backups + $created) {
    $fullPath = Join-Path $root $file
    if (Test-Path $fullPath) {
        Write-Host "OK " + $file
        $stat = Get-Item $fullPath
        Write-Host "  Size: $($stat.Length) bytes"
        Write-Host ""
    }
}

Write-Host "=== SUMMARY OF CHANGES ==="
Write-Host ""
Write-Host "1. Android:"
Write-Host "   - ApplicationId changed to: orbisextract.app"
Write-Host "   - ApplicationVersion changed to: 1.3"
Write-Host "   - ApplicationDisplayVersion changed to: 1.3"
Write-Host "   - android:label changed to: OrbisExtract"
Write-Host ""
Write-Host "2. Desktop Windows:"
Write-Host "   - assemblyIdentity version changed to: 1.3.0.0"
Write-Host "   - Added Avalonia resource support (Assets)"
Write-Host "   - Icon configured via Icon.ico (needs to be created)"
Write-Host ""
Write-Host "3. Version:"
Write-Host "   - Updated to 1.3 in all relevant files"
Write-Host ""
Write-Host "Backup files created: $([int]$backups.Count)"
