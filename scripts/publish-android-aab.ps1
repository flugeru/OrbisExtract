param(
    [Parameter(Mandatory = $true)]
    [string]$KeyStore,
    [string]$KeyAlias = 'Tatsuya',
    [string]$AndroidSdkDirectory = (Join-Path $env:LOCALAPPDATA 'Android\Sdk')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App.Android\OrbisExtract.App.Android.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$expectedVersionCode = [string]$projectXml.Project.PropertyGroup.ApplicationVersion
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
function Get-SingleCertificate([string[]]$pemOutput, [string]$description) {
    $pem = $pemOutput -join "`n"
    $matches = [regex]::Matches($pem, '(?s)-----BEGIN CERTIFICATE-----\s*(.*?)\s*-----END CERTIFICATE-----')
    if ($matches.Count -ne 1) { throw "Expected exactly one certificate from $description; found $($matches.Count)." }
    $der = [Convert]::FromBase64String(($matches[0].Groups[1].Value -replace '\s', ''))
    return [Security.Cryptography.X509Certificates.X509Certificate2]::new($der)
}

try {
    $storePassword = Read-Host 'Keystore password' -AsSecureString
    $keyPassword = Read-Host 'Key password (often the same as the keystore password)' -AsSecureString
    New-Item -ItemType Directory -Path $passwordDir | Out-Null
    Write-SecretFile $storePassword $storePasswordFile
    Write-SecretFile $keyPassword $keyPasswordFile
    $storePassword = $null
    $keyPassword = $null

    # Compare public certificates without printing or sharing the keystore password.
    $keyCertificateOutput = & keytool -exportcert -rfc -keystore $keyFile -alias $KeyAlias "-storepass:file" $storePasswordFile 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Cannot read upload certificate for alias $KeyAlias from the keystore." }
    $keyCertificate = Get-SingleCertificate $keyCertificateOutput 'upload keystore'
    if ($keyCertificate.Subject -match 'CN=Android Debug') { throw 'The selected upload key is an Android Debug certificate.' }

    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # Android signing is incremental; a prior debug-signed bundle may otherwise be copied unchanged.
    dotnet clean $project -f net10.0-android -c Release "-p:AndroidSdkDirectory=$AndroidSdkDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'Android Release clean failed.' }
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
    $bundleCertificateOutput = & keytool -printcert -jarfile $signed[0].FullName -rfc 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read the AAB signer certificate.' }
    $bundleCertificate = Get-SingleCertificate $bundleCertificateOutput 'signed AAB'
    if ($bundleCertificate.Subject -match 'CN=Android Debug' -or
        $bundleCertificate.Thumbprint -ne $keyCertificate.Thumbprint) {
        throw 'AAB signer does not match the upload keystore certificate. Do NOT share this AAB.'
    }
    $signedApk = @(Get-ChildItem -LiteralPath $output -File -Filter '*-Signed.apk')
    if ($signedApk.Count -ne 1) { throw 'Expected a companion APK to verify package and SDK versions.' }
    $aapt = Join-Path $AndroidSdkDirectory 'build-tools\36.0.0\aapt.exe'
    $badging = & $aapt dump badging $signedApk[0].FullName
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Android package manifest.' }
    $package = $badging | Where-Object { $_ -match '^package:' } | Select-Object -First 1
    $sdk = $badging | Where-Object { $_ -match '^sdkVersion:' } | Select-Object -First 1
    $target = $badging | Where-Object { $_ -match '^targetSdkVersion:' } | Select-Object -First 1
    if ($package -notmatch "name='com\.tatsuya\.orbisextract'" -or
        $package -notmatch "versionCode='$([regex]::Escape($expectedVersionCode))'" -or
        $sdk -notmatch "'24'" -or $target -notmatch "'36'") {
        throw "Unexpected package settings: $package / $sdk / $target"
    }
    Write-Host "$package / $sdk / $target"
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $fingerprint = [BitConverter]::ToString($sha256.ComputeHash($bundleCertificate.RawData)) }
    finally { $sha256.Dispose() }
    Write-Host "Upload certificate SHA-256: $fingerprint"
    Write-Host "Signed AAB ready: $($signed[0].FullName)"
}
finally {
    Remove-Item -LiteralPath $storePasswordFile,$keyPasswordFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $passwordDir -Force -ErrorAction SilentlyContinue
}
