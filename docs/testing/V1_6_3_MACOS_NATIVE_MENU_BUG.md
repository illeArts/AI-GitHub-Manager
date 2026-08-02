# macOS native menu repair (v1.6.3)

## Root cause

The app used Avalonia 11.2.3's `NativeMenu.SetMenu(Application.Current, …)`
from `OnFrameworkInitializationCompleted()`, and supplied a whole menu bar
(application menu plus Help) at that point. This is not how the Avalonia
Native exporter consumes this attached property:

- The application-level exporter reads `NativeMenu.GetMenu(Application.Current)`
  while the native platform is being initialized. It uses that menu as the
  *contents* of the macOS application menu and appends the standard Services,
  Hide, Show All, and Quit entries.
- A later `SetMenu` on `Application` does not notify this exporter. The
  diagnostic that merely counted the managed menu objects therefore did not
  prove that macOS received it.
- Additional visible root menus belong on the `Window`/`TopLevel` native-menu
  exporter. The old code thus nested the application menu incorrectly and
  could not reliably expose Help as a menu-bar root item.

## Repair

`App` now creates and attaches the application menu in its constructor, before
Avalonia.Native initializes its exporter. It contains About and Preferences
(⌘,); Avalonia supplies the platform-standard Services/Hide/Quit items,
including ⌘Q. Once `MainWindow` exists, a window-level native menu supplies
the separate Help root with Help (⌘?), the GitHub project, and release notes.

The menu labels subscribe to `L.Changed`; language selection remains stored by
`AppSettingsService` and is applied at startup. The temporary startup log that
exposed implementation diagnostics was removed.

## Verification performed on macOS

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `DOTNET_ROLL_FORWARD=Major dotnet test -c Release`: 249/249 passed. The
  roll-forward is required on this test Mac because only the .NET 10 runtime
  is installed; the project continues to target .NET 8.
- `bash scripts/verify-macos-app-bundle.sh`: arm64 and x64 bundles passed
  `plutil`, architecture, deep strict `codesign`, ZIP unpacking, and a second
  deep strict signature verification.
- The arm64 `.app` was started with `open`. macOS Accessibility inspection
  reported the menu-bar roots `Apple, AI GitHub Manager, Hilfe`; the app menu
  contained About, Preferences, Services, Hide, Hide Others, Show All and
  Quit, and Help contained Help, GitHub project, and Release Notes. Activating
  About opened the About window; activating Preferences opened Settings.

`spctl` remains informationally rejected for the release artifacts because
they are ad-hoc signed and not notarized.
