<#
.SYNOPSIS
    Wraps the signed MSIX bundle into a single double-click ClaudeUsageSetup.exe using Inno Setup —
    the "just make an .exe" path: end users get one file, a normal wizard, and an entry in
    "Apps & features", with no PowerShell or certificate knowledge required.

.DESCRIPTION
    Requires Inno Setup 6 (free, https://jrsoftware.org/isinfo.php) to be installed on THIS
    machine — the machine building the release, not the end user's machine. Run
    New-PackagingCertificate.ps1 and Build-MsixPackage.ps1 first (or let this script run
    Build-MsixPackage.ps1 for you via -BuildBundle).

.EXAMPLE
    ./Build-Installer.ps1
    Compiles installer/ClaudeUsage.iss, assuming dist/ClaudeUsage.msixbundle already exists.

.EXAMPLE
    ./Build-Installer.ps1 -BuildBundle -Platform x64
    Also (re)builds and signs the MSIX bundle first.
#>
param(
    [switch]$BuildBundle,

    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$bundlePath = Join-Path $repoRoot "dist\ClaudeUsage.msixbundle"
$certPath = Join-Path $repoRoot "certs\ClaudeUsage.cer"
$issPath = Join-Path $repoRoot "installer\ClaudeUsage.iss"

if ($BuildBundle) {
    & (Join-Path $PSScriptRoot "Build-MsixPackage.ps1") -Platform $Platform
}

if (-not (Test-Path $bundlePath)) {
    throw "'$bundlePath' not found. Run Build-MsixPackage.ps1 first (or pass -BuildBundle)."
}
if (-not (Test-Path $certPath)) {
    throw "'$certPath' not found. Run New-PackagingCertificate.ps1 first."
}

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) { $iscc = $onPath.Source }
}
if (-not $iscc) {
    throw "ISCC.exe (Inno Setup Compiler) not found. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php and try again."
}

Write-Host "Compiling installer with: $iscc" -ForegroundColor Cyan
& $iscc $issPath

if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "Done: dist\ClaudeUsageSetup.exe" -ForegroundColor Green
Write-Host "This single file is everything an end user needs — double-click, click through the wizard, done."
