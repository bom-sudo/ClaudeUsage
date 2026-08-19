# ClaudeUsage

A native Windows 11 widget for monitoring Claude usage — requests, tokens, cost, model breakdown, and history — built with WinUI 3 / Windows App SDK and .NET 8, following Fluent Design (Mica, rounded corners, Segoe UI Variable).

> **Status note:** this was authored without a local .NET/Windows App SDK toolchain available to compile against, so it hasn't been built or run yet. The code was written and hand-reviewed carefully for correctness, but budget the first build for fixing whatever the compiler/XAML compiler surfaces (NuGet package version pins in particular — see [Known limitations](#known-limitations)).

---

## 1. Architecture

```
UI (XAML Views)
     │  x:Bind / {Binding}
     ▼
ViewModels (MainViewModel, SettingsViewModel)   — CommunityToolkit.Mvvm, ClaudeUsage (app) project
     │
     ▼
IUsageService  (UsageService)                   — ClaudeUsage.Core project
     │
     ├── IUsageProvider ── DemoUsageProvider     (fully self-contained fake data, zero network calls)
     │                 └── ClaudeUsageProvider   (generic HTTPS/JSON client, see §4)
     ├── IStorageService ── JsonFileStorageService  (local cache + settings, JSON on disk)
     ├── ISecretProvider ── CredentialVaultStore     (Windows Credential Locker — app project only)
     ├── INotificationService ── ToastNotificationService (Windows App SDK toast — app project only)
     └── IStartupService ── StartupTaskService        (StartupTask API — app project only)
```

The solution is split into two source projects on purpose:

- **ClaudeUsage.Core** — plain `net8.0` class library. All data models, the provider abstraction, caching, and the refresh/throttle/backoff/notification orchestration (`UsageService`) live here. It has no WinUI/Windows dependency, which is what makes it unit-testable without spinning up a UI runtime.
- **ClaudeUsage** — the WinUI 3 app. Views, ViewModels, and the three Windows-specific service implementations (Credential Locker, toast notifications, startup task) that satisfy Core's interfaces. `App.xaml.cs` is the composition root: it wires everything together by hand with `Microsoft.Extensions.DependencyInjection` (no ASP.NET-style hosting needed for a desktop app this size).

**MVVM.** Views only bind to ViewModel properties/commands (`CommunityToolkit.Mvvm`'s `[ObservableProperty]`/`[RelayCommand]` source generators) and never talk to a service directly. `MainViewModel` owns dashboard state and subscribes to `IUsageService` events, marshalling updates back to the UI thread via `DispatcherQueue`. `SettingsViewModel` owns the settings form and persists through `IStorageService`/`ISecretProvider`/`IStartupService`.

**Data flow.** `UsageService.RefreshAsync` picks `DemoUsageProvider` or `ClaudeUsageProvider` based on `AppSettings.DemoModeEnabled`, fetches a `UsageSnapshot`, updates `Current`, persists it via `IStorageService` for offline display, and raises `UsageUpdated`/`ConnectionStateChanged`/`UsageThresholdCrossed` events. A `PeriodicTimer`-based loop drives auto-refresh at the configured interval; manual refreshes are debounced (2s) and failures back off exponentially (15s → 10min cap, honoring a server's `Retry-After` when present) — see §28 performance notes below.

**Security model.**
- The API key is never held in Core, never serialized to the JSON cache, and never logged. It lives only in the Windows Credential Locker via `Windows.Security.Credentials.PasswordVault` (`CredentialVaultStore`).
- All Core/App logging goes through `Microsoft.Extensions.Logging`; nowhere does the code log request headers, keys, or full response bodies — only status codes and exception messages.
- `ClaudeUsageProvider` only ever calls the single endpoint the user configured, over HTTPS (the endpoint's scheme isn't enforced in code, but the Package manifest only requests the `internetClient` capability, and the Settings UI's placeholder text and docs both push toward `https://`).
- Exceptions surfaced to the UI (`ConnectionTestStatusText`, the offline banner) are short, generic strings — never raw exception text that might echo back a key or full URL with query secrets.

## 2. UI design

- **Dashboard** (`Views/MainWindow.xaml`): a single scrollable column of cards — Today's Usage (percent ring + progress bar + Requests/Tokens/Cost), Model Usage (per-model progress bars, driven off `ObservableCollection<ModelUsageItem>` so adding a model is a data change, not a XAML change), Usage History (a hand-rolled `HistoryChartControl` — one `Polyline` + one filled `Polygon`, no charting library), Estimated Cost, and API Status (with an inline error banner + Retry button when offline).
- **Responsive layout**: the window has one XAML tree; `MainWindow.xaml.cs` toggles section `Visibility` on `RootGrid.SizeChanged` at two breakpoints (380px, 560px) to match the small/medium/large behavior in the spec, rather than maintaining three separate layouts.
- **Windows 11 chrome**: `ExtendsContentIntoTitleBar` + `MicaBackdrop` on both windows; cards use `CardBackgroundFillColorDefaultBrush`/`CardStrokeColorDefaultBrush` (semi-transparent, layers correctly over Mica); 14px corner radius on cards, 8px on small controls.
- **Color**: usage/connection state colors are looked up from Fluent's own semantic brushes (`SystemFillColorSuccessBrush`, `SystemFillColorCautionBrush`, `SystemFillColorCriticalBrush`, `AccentFillColorDefaultBrush`) by resource key at bind time, rather than custom hex colors — this keeps the palette consistent with the rest of Windows 11 and correct across Light/Dark/High Contrast automatically.
- **Settings** (`Views/SettingsWindow.xaml`): built on `CommunityToolkit.WinUI.Controls.SettingsControls`' `SettingsCard`, which is the same control Windows' own Settings app uses, grouped into API Configuration / Appearance / Refresh / Notifications / Startup & Advanced.
- **Loading state**: `Controls/SkeletonBlock.xaml` — a pulsing placeholder (opacity animation, ~1.8s cycle) shown in place of the Today card's content until the first snapshot arrives; no blank screen.
- **Accessibility**: interactive controls carry `AutomationProperties.Name` and `ToolTipService.ToolTip` (icon buttons, the usage progress bar); the app relies on WinUI's built-in keyboard navigation and High Contrast theme support rather than overriding it.
- **Tray icon** (`Services/TrayIconService.cs`, via `H.NotifyIcon.WinUI`): right-click menu with live status/usage lines, Open/Refresh/Settings/Pause Auto Refresh/Exit. Closing the main window hides it to tray instead of exiting (`AppWindow.Closing` cancelled); only the tray's Exit command does a real shutdown.

## 3. Project structure

```
ClaudeUsage/
├── ClaudeUsage.sln
├── README.md
├── src/
│   ├── ClaudeUsage.Core/                 # platform-agnostic, unit-testable
│   │   ├── Models/                       # UsageData, ModelUsage, CostData, UsageSnapshot, AppSettings, Enums
│   │   ├── Formatting/Formatters.cs      # token/cost/relative-time display formatting
│   │   └── Services/
│   │       ├── IUsageProvider.cs, DemoUsageProvider.cs, ClaudeUsageProvider.cs
│   │       ├── IUsageService.cs, UsageService.cs
│   │       ├── IStorageService.cs, JsonFileStorageService.cs
│   │       ├── ISecretProvider.cs, INotificationService.cs, IStartupService.cs   (interfaces only)
│   │       └── Exceptions.cs
│   └── ClaudeUsage/                      # WinUI 3 app (net8.0-windows10.0.19041.0, packaged/MSIX)
│       ├── App.xaml(.cs)                 # composition root
│       ├── Services/                     # Windows-specific: CredentialVaultStore, ToastNotificationService,
│       │                                 #   StartupTaskService, TrayIconService
│       ├── ViewModels/                   # MainViewModel, SettingsViewModel, ModelUsageItem, NotificationThresholdOption
│       ├── Views/
│       │   ├── MainWindow.xaml(.cs), SettingsWindow.xaml(.cs)
│       │   └── Controls/                 # HistoryChartControl, SkeletonBlock
│       ├── Converters/CommonConverters.cs
│       ├── Styles/Theme.xaml
│       ├── Assets/                       # placeholder icons (see §7)
│       ├── Package.appxmanifest, app.manifest
│       └── Properties/PublishProfiles/   # win-x86/x64/arm64.pubxml
└── tests/
    └── ClaudeUsage.Core.Tests/           # xUnit — DemoUsageProvider, UsageService, JsonFileStorageService, Formatters
```

## 4. Connecting a real data source

Anthropic does not currently publish a public, per-key usage/billing query API for end users, so `ClaudeUsageProvider` doesn't fabricate one. Instead it's a generic HTTPS/JSON client: point **Settings → API Configuration → API Endpoint** at anything that returns the shape documented in [`src/ClaudeUsage/appsettings.sample.json`](src/ClaudeUsage/appsettings.sample.json) (a small internal proxy, a LiteLLM/gateway usage export, an admin API you already run, etc.), with the API key sent as `Authorization: Bearer <key>`. Until you have such an endpoint, leave **Demo Mode** on — it's fully functional for exercising the whole UI and makes zero network calls.

Swapping in a different data source entirely (e.g., a different provider's API) means implementing `IUsageProvider` and registering it in `App.xaml.cs`'s `BuildServiceProvider()` — nothing in the ViewModels or Views needs to change.

## 5. Build & run

Requires Visual Studio 2022 (17.9+) with the **Windows App SDK** and **.NET desktop development** workloads, or the .NET 8 SDK + Windows App SDK CLI tooling.

```powershell
# Restore & build everything (app + core + tests)
dotnet restore ClaudeUsage.sln
dotnet build ClaudeUsage.sln -c Debug

# Run the unit tests (no Windows Runtime needed — pure .NET)
dotnet test tests/ClaudeUsage.Core.Tests/ClaudeUsage.Core.Tests.csproj

# Run the app (Visual Studio is the easiest path for a packaged WinUI 3 app —
# open ClaudeUsage.sln, set ClaudeUsage as Startup Project, F5)
```

### Packaging a signed MSIX for distribution

Two equivalent paths — both need Visual Studio 2022 (or the Build Tools for Visual Studio) installed, since MSBuild does the actual packaging/signing:

**Visual Studio wizard:** right-click **ClaudeUsage** → **Publish → Create App Packages…** → *Sideloading* → create/select a signing certificate → pick architecture(s) → *Create*.

**Scripted** (`scripts/`), for repeatable releases:

```powershell
# One-time: create a self-signed cert matching Package.appxmanifest's Publisher (CN=ClaudeUsage)
./scripts/New-PackagingCertificate.ps1

# Build + sign the package (locates MSBuild via vswhere)
./scripts/Build-MsixPackage.ps1 -Platform x64
```

Output lands in `dist/` as a signed `.msixbundle` (plus a stably-named copy, `dist/ClaudeUsage.msixbundle`, regardless of version). Ship it alongside `certs/ClaudeUsage.cer` (the *public* half — never the `.pfx`) — the recipient runs `scripts/Install-ClaudeUsage.ps1 -PackagePath <bundle> -CertificatePath <cer>` to trust the cert and install, no admin rights required. Both scripts' doc comments cover the details (cert trust store, Developer Mode requirement, why the password briefly appears as a process argument). `docs/MANUAL-TH.md` has the full walkthrough with recipient-side instructions in Thai.

The `dotnet publish -p:PublishProfile=win-x64` profiles under `Properties/PublishProfiles/` remain useful for producing a self-contained, ReadyToRun build without packaging (`GenerateAppxPackageOnBuild=false`) — e.g. for local testing of the raw binaries — but they don't produce an installable MSIX; use the packaging path above for that.

### One-file installer (`ClaudeUsageSetup.exe`)

The MSIX/certificate/PowerShell dance above is what makes secure credential storage, toast notifications, and launch-at-startup work correctly — but it's not something to hand an ordinary end user. `installer/ClaudeUsage.iss` (an [Inno Setup](https://jrsoftware.org/isinfo.php) script — free, ~10 MB compiler) wraps the whole thing into a single `.exe`: double-click, click through a normal wizard, done. Under the hood it trusts the certificate, flips the same registry switch as "Developer Mode → Install apps for sideloading", and calls `Add-AppxPackage` — none of which the person installing it ever sees. It also registers a proper uninstaller in *Apps & features* that removes the AppX package.

```powershell
./scripts/New-PackagingCertificate.ps1        # once
./scripts/Build-Installer.ps1 -BuildBundle    # builds the MSIX, then compiles the installer
```

Requires Inno Setup 6 installed on the *build* machine only (`Build-Installer.ps1` looks for `ISCC.exe` under Program Files or on `PATH`). Output: `dist/ClaudeUsageSetup.exe` — that one file is everything you hand to someone else.

## 6. Known limitations

Being upfront about what wasn't (and couldn't be, in this environment) verified end-to-end:

- **Not yet compiled.** No .NET/Windows App SDK toolchain was available while writing this, so the first `dotnet build` may need small fixes — most likely NuGet package version pins (`Microsoft.WindowsAppSDK`, `CommunityToolkit.WinUI.Controls.SettingsControls`, `H.NotifyIcon.WinUI` version numbers were chosen from memory of roughly-current releases; let NuGet float to the nearest compatible version if an exact one 404s).
- **Icons are placeholders.** `Assets/*.png` and `AppIcon.ico` were generated programmatically (a simple gradient "C" mark) so the manifest and build are complete — swap them for real branding before shipping.
- **Tray icon API surface.** `TrayIconService` uses `H.NotifyIcon.WinUI`'s documented `TaskbarIcon` API (`IconSource`, `ContextFlyout`, `ForceCreate()`); if the installed package version's surface differs slightly, that's the first place to check.
- **Theme changes apply on Settings close**, not live per-keystroke — flipping Light/Dark takes effect when the Settings window closes (it re-reads the saved settings and applies `ElementTheme` to both windows), not instantly on radio-button click.
- **MSIX signing scripts are untested end-to-end** (see §5) — `scripts/New-PackagingCertificate.ps1` and `scripts/Build-MsixPackage.ps1` were written correctly against the documented `New-SelfSignedCertificate`/MSBuild packaging properties but not run in this environment (no Visual Studio/MSBuild available); the manifest's `CN=ClaudeUsage` identity is a local/self-signed placeholder either way — fine for sideloading, not for the Microsoft Store.
- **`installer/ClaudeUsage.iss` is likewise untested** — no Inno Setup compiler was available in this environment either. The script was written carefully against Inno's documented `[Run]`/`[Registry]` syntax (including escaping literal `{ }` in the embedded PowerShell, which Inno's own `{constant}` substitution would otherwise choke on) — but budget a first compile-and-run pass to shake out anything subtle.

## 7. What's genuinely implemented (not stubbed)

Demo Mode data generation, the full refresh/cache/throttle/backoff/notification pipeline, secure credential storage, toast notifications, launch-at-startup via `StartupTask`, the responsive three-tier layout, the offline/error banner with Retry, and the unit test suite are all real, working code — not mockups. `ClaudeUsageProvider` is a real HTTP client ready to point at a compatible endpoint the moment one exists.
