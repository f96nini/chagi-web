param(
    [Parameter(Mandatory = $true)]
    [string]$KeystorePath,

    [Parameter(Mandatory = $true)]
    [string]$KeyAlias,

    [Parameter(Mandatory = $true)]
    [string]$PasswordFile,

    [string]$ProjectPath = ".\\ITF-Taekwondo-Gradering.csproj",
    [string]$ApplicationId,
    [int]$ApplicationVersion,
    [string]$ApplicationDisplayVersion
)

$ErrorActionPreference = "Stop"

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$resolvedKeystorePath = (Resolve-Path $KeystorePath).Path
$resolvedPasswordFile = (Resolve-Path $PasswordFile).Path

if (-not (Test-Path $resolvedProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

if (-not (Test-Path $resolvedKeystorePath)) {
    throw "Keystore file not found: $KeystorePath"
}

if (-not (Test-Path $resolvedPasswordFile)) {
    throw "Password file not found: $PasswordFile"
}

$publishArgs = @(
    "publish"
    $resolvedProjectPath
    "-f"
    "net9.0-android"
    "-c"
    "Release"
    "-p:AndroidKeyStore=true"
    "-p:AndroidPackageFormats=aab"
    "-p:AndroidSigningKeyStore=$resolvedKeystorePath"
    "-p:AndroidSigningKeyAlias=$KeyAlias"
    "-p:AndroidSigningKeyPass=file:$resolvedPasswordFile"
    "-p:AndroidSigningStorePass=file:$resolvedPasswordFile"
)

if ($ApplicationId) {
    $publishArgs += "-p:ApplicationId=$ApplicationId"
}

if ($PSBoundParameters.ContainsKey("ApplicationVersion")) {
    $publishArgs += "-p:ApplicationVersion=$ApplicationVersion"
}

if ($ApplicationDisplayVersion) {
    $publishArgs += "-p:ApplicationDisplayVersion=$ApplicationDisplayVersion"
}

Write-Host "Publishing signed Android App Bundle..."
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$publishDir = Join-Path (Split-Path $resolvedProjectPath -Parent) "bin\\Release\\net9.0-android\\publish"
Write-Host ""
Write-Host "Publish completed. AAB files:"
Get-ChildItem -Path $publishDir -Filter *.aab | Select-Object FullName, Length, LastWriteTime
