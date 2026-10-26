param(
    [Parameter(Mandatory = $true)]
    [string]$KeyStore,
    [string]$KeyAlias = 'orbisextract',
    [string]$AndroidSdkDirectory = (Join-Path $env:LOCALAPPDATA 'Android\Sdk')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App.Android\OrbisExtract.App.Android.csproj'
$sourceIcon = Join-Path $root 'OrbisExtract-square.png'
$projectIcon = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App.Android\Icon.png'
$output = Join-Path $root ('artifacts\play-upload\' + [guid]::NewGuid().ToString('N'))
$keyFile = (Resolve-Path -LiteralPath $KeyStore).Path
if ($keyFile.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep the upload keystore outside the repository.'
}
if (-not (Test-Path -LiteralPath (Join-Path $AndroidSdkDirectory 'platforms\android-36\android.jar') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $AndroidSdkDirectory 'build-tools\36.0.0\aapt.exe') -PathType Leaf)) {
    throw "Android SDK not found at $AndroidSdkDirectory"
}
if ($KeyAlias -notmatch '^[a-zA-Z0-9_.-]+$') { throw 'Invalid key alias.' }
if ((Get-FileHash -LiteralPath $sourceIcon -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $projectIcon -Algorithm SHA256).Hash) {
    throw 'Android Icon.png differs from OrbisExtract-square.png. Synchronize the icons before publishing.'
}

# The Android AAB signer requires file: password references, not env: references.
# Keep these temporary files out of the repository and remove them even on failure.
$passwordDir = Join-Path $env:LOCALAPPDATA ('Temp\OrbisExtract-AAB-' + [guid]::NewGuid().ToString('N'))
$storePasswordFile = Join-Path $passwordDir 'store-password'
$keyPasswordFile = Join-Path $passwordDir 'key-password'
function Write-SecretFile([Security.SecureString]$secret, [string]$path) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    try {
        [IO.File]::WriteAllText($path, [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr), [Text.UTF8Encoding]::new($false))
    }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

try {
    $storePassword = Read-Host 'Keystore password' -AsSecureString
    $keyPassword = Read-Host 'Key password (often the same as the keystore password)' -AsSecureString
    New-Item -ItemType Directory -Path $passwordDir | Out-Null
    Write-SecretFile $storePassword $storePasswordFile
    Write-SecretFile $keyPassword $keyPasswordFile
    $storePassword = $null
    $keyPassword = $null

    New-Item -ItemType Directory -Force -Path $output | Out-Null
    dotnet restore $project "-p:AndroidSdkDirectory=$AndroidSdkDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'Android restore failed.' }

    dotnet publish $project -f net10.0-android -c Release --no-restore `
        '-p:AndroidPackageFormat=aab' '-p:AndroidKeyStore=true' `
        "-p:AndroidSigningKeyStore=$keyFile" "-p:AndroidSigningKeyAlias=$KeyAlias" `
        "-p:AndroidSigningStorePass=file:$storePasswordFile" `
        "-p:AndroidSigningKeyPass=file:$keyPasswordFile" `
        "-p:AndroidSdkDirectory=$AndroidSdkDirectory" `
        -o $output
    if ($LASTEXITCODE -ne 0) { throw 'AAB publish failed.' }

    $signed = @(Get-ChildItem -LiteralPath $output -File -Filter '*-Signed.aab')
    if ($signed.Count -ne 1) { throw "Expected exactly one signed AAB in $output; found $($signed.Count)." }
    & jarsigner -verify $signed[0].FullName
    if ($LASTEXITCODE -ne 0) { throw 'Signed AAB verification failed.' }
    Write-Host "Signed AAB ready: $($signed[0].FullName)"
}
finally {
    Remove-Item -LiteralPath $storePasswordFile,$keyPasswordFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $passwordDir -Force -ErrorAction SilentlyContinue
}
