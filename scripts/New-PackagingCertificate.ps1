<#
.SYNOPSIS
    Creates a self-signed code-signing certificate for sideloading the ClaudeUsage MSIX package.

.DESCRIPTION
    The certificate's Subject must exactly match the Publisher attribute in
    src/ClaudeUsage/Package.appxmanifest ("CN=ClaudeUsage") or packaging will fail.
    This is for local/internal sideloading only — for the Microsoft Store, use Partner
    Center's own signing instead of this script.

.EXAMPLE
    ./New-PackagingCertificate.ps1
    Prompts for a .pfx password and writes ClaudeUsage.pfx / ClaudeUsage.cer to ../certs.
#>
param(
    [string]$Subject = "CN=ClaudeUsage",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\certs"),
    [securestring]$Password
)

$ErrorActionPreference = "Stop"

if (-not $Password) {
    $Password = Read-Host "Enter a password to protect the new .pfx file" -AsSecureString
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "ClaudeUsage packaging certificate" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pfxPath = Join-Path $OutputDirectory "ClaudeUsage.pfx"
$cerPath = Join-Path $OutputDirectory "ClaudeUsage.cer"

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $Password | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

Write-Host ""
Write-Host "Certificate created (Subject: $Subject, Thumbprint: $($cert.Thumbprint))" -ForegroundColor Green
Write-Host "  Private key + cert (keep secret, used to SIGN the package): $pfxPath"
Write-Host "  Public certificate (ship this to every machine that will INSTALL the package): $cerPath"
Write-Host ""
Write-Host "Next: run Build-MsixPackage.ps1 to build and sign the package with this certificate."
Write-Host "Do not commit certs/ClaudeUsage.pfx to source control — it's already covered by .gitignore's *.pfx rule."
