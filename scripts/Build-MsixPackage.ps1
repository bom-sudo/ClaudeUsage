<#
.SYNOPSIS
    Builds and signs an MSIX package for ClaudeUsage from the command line — the scripted
    equivalent of Visual Studio's Project > Publish > Create App Packages wizard.

.DESCRIPTION
    Requires Visual Studio 2022 (or the Build Tools for Visual Studio) with the
    ".NET desktop development" and "Windows application development" workloads, since
    MSBuild is what actually does the packaging/signing work — this script just locates it
    and passes the right properties.

    Run New-PackagingCertificate.ps1 first if you don't already have certs/ClaudeUsage.pfx.

    Security note: the .pfx password is passed to MSBuild as a process argument, so it's
    briefly visible to other processes on the machine (e.g. Task Manager) while the build
    runs — the same tradeoff Visual Studio's own packaging wizard makes. Fine for local/manual
    releases; for CI, prefer a certificate-less signing step or a secrets-manager-backed runner.

.EXAMPLE
    ./Build-MsixPackage.ps1 -Platform x64
#>
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64',

    [string]$PfxPath = (Join-Path $PSScriptRoot "..\certs\ClaudeUsage.pfx"),

    [securestring]$Password,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\dist")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PfxPath)) {
    throw "Signing certificate not found at '$PfxPath'. Run New-PackagingCertificate.ps1 first."
}
$PfxPath = (Resolve-Path $PfxPath).Path

if (-not $Password) {
    $Password = Read-Host "Enter the .pfx password" -AsSecureString
}
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at '$vswhere'. This script must run on a machine with Visual Studio 2022 (or the Build Tools) installed."
}

$msbuildPath = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
    Select-Object -First 1
if (-not $msbuildPath) {
    throw "MSBuild.exe was not found via vswhere. Install the '.NET desktop development' and 'Windows application development' workloads in Visual Studio Installer."
}

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\src\ClaudeUsage\ClaudeUsage.csproj")).Path
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

Write-Host "Building and signing $Platform package with MSBuild at:`n  $msbuildPath" -ForegroundColor Cyan

& $msbuildPath $projectPath `
    /restore `
    /p:Configuration=Release `
    /p:Platform=$Platform `
    /p:AppxBundlePlatforms=$Platform `
    /p:AppxBundle=Always `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    "/p:AppxPackageDir=$OutputDirectory\" `
    /p:AppxPackageSigningEnabled=true `
    "/p:PackageCertificateKeyFile=$PfxPath" `
    "/p:PackageCertificatePassword=$plainPassword"

$exitCode = $LASTEXITCODE
$plainPassword = $null
[System.GC]::Collect()

if ($exitCode -ne 0) {
    throw "MSBuild failed with exit code $exitCode."
}

Write-Host ""
Write-Host "Package created under: $OutputDirectory" -ForegroundColor Green
Write-Host "Hand the recipient the .msixbundle (or platform-specific .msix) plus certs/ClaudeUsage.cer, then have them run Install-ClaudeUsage.ps1."
