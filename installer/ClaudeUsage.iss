; Inno Setup script — wraps the signed ClaudeUsage.msixbundle in a familiar double-click
; Setup.exe (wizard pages, Start Menu entry via the app's own AppX registration, an
; uninstaller in "Apps & features"). Compile with Inno Setup 6 (https://jrsoftware.org/isinfo.php):
;
;   ISCC.exe installer\ClaudeUsage.iss
;
; Prerequisites (on the machine compiling this, NOT the end user's machine):
;   1. Run scripts\New-PackagingCertificate.ps1 once
;   2. Run scripts\Build-MsixPackage.ps1 to produce dist\ClaudeUsage.msixbundle
;   3. Run this script (or scripts\Build-Installer.ps1, which does it for you)
;
; The end user only ever sees ClaudeUsageSetup.exe — no PowerShell, no certificates, no MSIX
; terminology. Because MSIX packages register their own Start Menu tile, this installer's job
; is just: trust the certificate, allow sideloading, and hand the bundle to Add-AppxPackage.

#define MyAppName "ClaudeUsage"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ClaudeUsage"
#define BundleFile "..\dist\ClaudeUsage.msixbundle"
#define CertFile "..\certs\ClaudeUsage.cer"

[Setup]
AppId={{6B6E9B7B-6B4B-4B7C-9B9C-2E1C7B7C9B41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
OutputDir=..\dist
OutputBaseFilename=ClaudeUsageSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\ClaudeUsage\Assets\AppIcon.ico
UninstallDisplayIcon={app}\AppIcon.ico
; Needs admin once, to allow sideloaded (non-Store) app installs via a machine policy key —
; the same effect as turning on "Developer Mode" or "Install apps for sideloading" by hand.
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#BundleFile}"; DestDir: "{app}"; DestName: "ClaudeUsage.msixbundle"; Flags: ignoreversion
Source: "{#CertFile}"; DestDir: "{app}"; DestName: "ClaudeUsage.cer"; Flags: ignoreversion
Source: "..\src\ClaudeUsage\Assets\AppIcon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Equivalent to Settings > Privacy & security > For developers > "Install apps for sideloading".
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"; \
    ValueType: dword; ValueName: "AllowAllTrustedApps"; ValueData: "1"; Flags: uninsdeletevalue

[Run]
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""Import-Certificate -FilePath '{app}\ClaudeUsage.cer' -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null"""; \
    StatusMsg: "Trusting the publisher certificate..."; Flags: runhidden

; Note: deliberately avoids a try/catch or any { } block here — Inno's own {constant}
; substitution syntax (e.g. {app} below) would misparse literal script braces.
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""Add-AppxPackage -Path '{app}\ClaudeUsage.msixbundle' -ForceApplicationShutdown *> '{app}\install.log'"""; \
    StatusMsg: "Installing ClaudeUsage..."; Flags: runhidden

[UninstallRun]
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""Get-AppxPackage -Name '*ClaudeUsage*' | Remove-AppxPackage -ErrorAction SilentlyContinue"""; \
    RunOnceId: "RemoveClaudeUsageAppx"; Flags: runhidden
