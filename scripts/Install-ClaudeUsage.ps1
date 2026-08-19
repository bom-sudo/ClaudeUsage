<#
.SYNOPSIS
    Installs a ClaudeUsage MSIX package that was signed with a self-signed certificate
    (i.e. built via Build-MsixPackage.ps1), by trusting the certificate and running Add-AppxPackage.

.DESCRIPTION
    Run this on the machine that will USE the app — not the machine that built it.
    You need both files the publisher gave you: the .msixbundle (or .msix) and the
    matching ClaudeUsage.cer. No administrator rights are required; the certificate is
    trusted for the current user only.

    If this machine has never sideloaded an app before, first enable it under
    Settings > Privacy & security > For developers > turn on "Developer Mode" (or the
    narrower "Install apps for sideloading" toggle on some Windows builds) — otherwise
    Add-AppxPackage fails even with a trusted certificate.

.EXAMPLE
    ./Install-ClaudeUsage.ps1 -PackagePath .\ClaudeUsage_1.0.0.0_x64.msixbundle -CertificatePath .\ClaudeUsage.cer
#>
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,

    [Parameter(Mandatory)]
    [string]$CertificatePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PackagePath)) {
    throw "Package not found at '$PackagePath'."
}
if (-not (Test-Path $CertificatePath)) {
    throw "Certificate not found at '$CertificatePath'."
}

Write-Host "Trusting the publisher's certificate for your user account..." -ForegroundColor Cyan
Import-Certificate -FilePath (Resolve-Path $CertificatePath).Path -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null

Write-Host "Installing $PackagePath ..." -ForegroundColor Cyan
Add-AppxPackage -Path (Resolve-Path $PackagePath).Path

Write-Host ""
Write-Host "Done — ClaudeUsage is installed. Look for it in the Start menu." -ForegroundColor Green
Write-Host "To remove it later: Get-AppxPackage *ClaudeUsage* | Remove-AppxPackage"
