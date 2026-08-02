# macOS-App-Bundle-Fix — v1.6.2 (Branch `fix/v1.6.2-macos-app-bundle`)

## Root Cause

Der `release-v1.6.2.yml`-Workflow baute bisher ausschließlich Windows- und
Linux-Pakete (`portable-packages`-Matrix: `windows-x64`, `linux-x64`) sowie
den Windows-Installer. **Es gab überhaupt keinen macOS-Build-Job.** Die
Release-Notes von v1.6.2 versprachen zwar `AI-GitHub-Manager-v1.6.2-macos-x64.zip`
und `-macos-arm64.zip`, tatsächlich fehlten diese Assets im veröffentlichten
GitHub-Release komplett (per GitHub-API am 2026-08-02 verifiziert: Release
`v1.6.2` enthielt nur `linux-x64.tar.gz`, `windows-x64.zip` und den
Windows-Installer).

Zusätzlich war die lokale Erstellung (`build-installer-mac.sh`) fehlerhaft:

1. Wenn `dotnet publish` kein `.app`-Bundle erzeugte (Normalfall bei
   `PublishSingleFile=true` auf macOS-RIDs — es entsteht nur eine nackte
   ausführbare Datei plus lose `.dylib`-Dateien), baute das Skript zwar ein
   Bundle, aber:
   - `CFBundleExecutable` zeigte auf `AI GitHub Manager` (mit Leerzeichen),
     während `AssemblyName` in der `.csproj` `AI.GitHubManager.App` heißt.
   - Es wurde nur `logo.png` als Icon-Platzhalter kopiert, kein echtes
     `AppIcon.icns`.
   - Die Avalonia-Native-Bibliotheken (`libAvaloniaNative.dylib`,
     `libHarfBuzzSharp.dylib`, `libSkiaSharp.dylib`) wurden nicht mit ins
     Bundle kopiert — die App wäre beim Start abgestürzt.
2. `build-installer-mac.sh` hatte durchgängig **CRLF-Zeilenenden** (Windows)
   im Repository. Das bricht die Ausführung mit `bash` auf einem echten Mac
   (`syntax error near unexpected token`).

Ergebnis für den Endnutzer: Beim Entpacken eines (manuell erzeugten oder
zukünftig automatisiert erzeugten) macOS-ZIPs erschien keine echte `.app`,
sondern nur eine lose Binärdatei — Finder kann das nicht per Doppelklick
starten.

Die Update-Erkennung (`PlatformDescriptor`, `ReleaseAssetSelector`) war
davon **nicht** betroffen — sie war bereits vor diesem Fix korrekt
plattform- und architektursicher implementiert und erwartet exakt die
Dateinamen `AI-GitHub-Manager-v<version>-macos-x64.zip` /
`-macos-arm64.zip`, die dieser Fix nun tatsächlich liefert.

## Geänderte Dateien

| Datei | Änderung |
|---|---|
| `.github/workflows/release-v1.6.2.yml` | Neuer Job `macos-packages` (Matrix arm64/x64, `runs-on: macos-latest`): publish → echtes `.app`-Bundle zusammenbauen → `plutil -lint` → Ad-hoc-`codesign` → Architektur-/Windows-Binary-Check → `ditto`-Zip → SHA-256 → Entpack-Gegenprobe → Upload. `release`-Job wartet jetzt zusätzlich auf `macos-packages`. Titel umbenannt auf „Windows, macOS and Linux". Auskommentierte, vorbereitete Schritte für Developer-ID-Signierung + Notarisierung via GitHub Secrets. |
| `build-installer-mac.sh` | CRLF → LF korrigiert. Bundle-Erzeugung überarbeitet: `CFBundleExecutable` = `AI.GitHubManager.App` (statt Name mit Leerzeichen), echtes `AppIcon.icns` statt PNG-Platzhalter, `.dylib`-Dateien werden mitkopiert, `plutil -lint` + Ad-hoc-`codesign` am Ende. |
| `src/AI.GitHubManager.App/Assets/AppIcon.icns` | Neu erzeugt aus `Assets/logo.png` (Icon-Größen 16–1024 px, inkl. @2x-Varianten). |
| `src/AI.GitHubManager.App/AI.GitHubManager.App.csproj` | `AppIcon.icns` von `AvaloniaResource`-Glob ausgeschlossen (ist Packaging-Input, kein App-Resource). |
| `scripts/verify-macos-app-bundle.sh` | Neu: lokales Verifikationsskript für den Mac — baut beide Architekturen, prüft `file`, `plutil -lint`, `codesign --verify`, `spctl --assess`, zippt mit `ditto`, entpackt zur Gegenprobe. |
| `docs/testing/MACOS_APP_BUNDLE_FIX_v1.6.2.md` | Diese Datei. |

Keine Versionsnummer wurde von 1.6.2 auf 1.6.3 geändert (geprüft per
`grep -rn "1.6.3"` — einzige Treffer sind Doku-Vermerke für zukünftige
Releases in `docs/testing/RELEASE_HISTORY.md` und `RELEASE_QA_v1.6.2.md`,
unverändert gelassen).

## Ausgeführte Befehle (in dieser Sitzung, Linux-Sandbox mit .NET 8 SDK 8.0.423)

```bash
git status && git switch main && git pull --ff-only origin main   # HEAD → c2253ab8f8b06af4e4d8498cda4a3867ea435440
git switch -c fix/v1.6.2-macos-app-bundle

dotnet restore AI.GitHubManager.sln
dotnet build   AI.GitHubManager.sln -c Release --no-restore
dotnet test    AI.GitHubManager.sln -c Release --no-build --verbosity normal   # 159/159 grün

dotnet publish src/AI.GitHubManager.App/AI.GitHubManager.App.csproj -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/osx-arm64

dotnet publish src/AI.GitHubManager.App/AI.GitHubManager.App.csproj -c Release -r osx-x64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/osx-x64

file artifacts/publish/osx-arm64/AI.GitHubManager.App   # Mach-O 64-bit arm64 executable
file artifacts/publish/osx-x64/AI.GitHubManager.App     # Mach-O 64-bit x86_64 executable

# Bundle je Architektur zusammengebaut (Contents/MacOS, Contents/Resources, Info.plist)
# Info.plist validiert via python3 -c "import plistlib; plistlib.load(...)" (plutil nicht
# verfügbar außerhalb macOS — siehe Einschränkungen unten)

zip -r -X -y "AI-GitHub-Manager-v1.6.2-macos-arm64.zip" "AI GitHub Manager.app"
zip -r -X -y "AI-GitHub-Manager-v1.6.2-macos-x64.zip"   "AI GitHub Manager.app"
sha256sum *.zip > entsprechende .sha256-Dateien

unzip AI-GitHub-Manager-v1.6.2-macos-arm64.zip -d /tmp/zipcheck   # Gegenprobe
file "AI GitHub Manager.app/Contents/MacOS/AI.GitHubManager.App"  # Mach-O arm64
```

## Einschränkung dieser Sitzung: macOS-exklusive Werkzeuge

Diese Sitzung lief in einer **Linux-Sandbox** (aarch64, Ubuntu 22.04) ohne
Zugriff auf echte macOS-Werkzeuge. Das .NET 8 SDK wurde manuell nachinstalliert,
Cross-Publish für `osx-arm64`/`osx-x64` funktioniert von Linux aus einwandfrei
(reine .NET-Funktion, kein Xcode nötig). **Nicht verfügbar waren:**
`codesign`, `spctl`, `plutil`, `ditto`, `open`, `iconutil`. Diese Schritte
wurden **nicht real ausgeführt** und müssen auf dem echten Mac nachgeholt
werden — dafür liegt `scripts/verify-macos-app-bundle.sh` bereit, das exakt
dieselben Schritte wie der neue CI-Job durchführt, inklusive:

- `file` (Architektur-Check)
- `plutil -lint` (Info.plist-Validierung)
- `codesign --force --deep --sign -` (Ad-hoc-Signatur) + `codesign --verify --deep --strict`
- `spctl --assess --type execute` (Gatekeeper-Bewertung — wird bei reiner
  Ad-hoc-Signatur voraussichtlich **ablehnen**, das ist erwartet und kein Fehler)
- `ditto -c -k --sequesterRsrc --keepParent` (statt losem `zip`)
- Entpack-Gegenprobe + `open`

**Bitte auf dem Mac ausführen, bevor irgendetwas released wird:**

```bash
cd ~/Documents/AAIAGitHub/Codex/"AI GitHub Manager"
git fetch && git switch fix/v1.6.2-macos-app-bundle
bash scripts/verify-macos-app-bundle.sh
open "artifacts/appbundle/osx-arm64/AI GitHub Manager.app"   # bzw. osx-x64 auf Intel-Mac
```

Das ZIP wurde in dieser Sitzung mit dem Linux-`zip`-Kommando erzeugt
(Unix-Rechte/Executable-Bit bleiben erhalten, siehe Entpack-Gegenprobe oben)
— **nicht** mit `ditto`. Der neue CI-Job und das Verifikationsskript nutzen
beide echtes `ditto`. Für den finalen Release-Build bitte das lokal auf dem
Mac mit `ditto` erzeugte ZIP verwenden, nicht das aus dieser Sitzung.

## Testergebnisse

- `dotnet test` (net8.0, Linux-Sandbox): **159/159 bestanden**, 0 Fehler,
  0 Warnungen beim Build.
- Architektur-Check: `osx-arm64`-Binary ist Mach-O arm64,
  `osx-x64`-Binary ist Mach-O x86_64 — jeweils bestätigt.
- Keine `.exe`/`.msi`-Dateien im macOS-Bundle gefunden.
- Executable-Bit nach ZIP→Unzip-Roundtrip erhalten.
- `Info.plist` beider Bundles: wohlgeformtes XML, `CFBundleShortVersionString`
  = `1.6.2`, `CFBundleExecutable` = `AI.GitHubManager.App`.
- **Nicht getestet in dieser Sitzung** (siehe Einschränkung oben):
  `codesign --verify`, `spctl --assess`, tatsächlicher Start via
  `open`/Finder-Doppelklick, `ditto`-Zip-Erstellung.

## Erzeugte Artefakte (in dieser Sitzung, liegen im Arbeitsbaum, nicht committet)

- `artifacts/publish/osx-arm64/`, `artifacts/publish/osx-x64/` — Publish-Output
- `artifacts/appbundle/osx-arm64/AI GitHub Manager.app`,
  `artifacts/appbundle/osx-x64/AI GitHub Manager.app` — fertige Bundles
- `artifacts/dist/AI-GitHub-Manager-v1.6.2-macos-arm64.zip` (+ `.sha256`)
- `artifacts/dist/AI-GitHub-Manager-v1.6.2-macos-x64.zip` (+ `.sha256`)

Diese Artefakte liegen unter `artifacts/`, `publish/`, `dist/` — allesamt
bereits in `.gitignore` erfasst (`dist/`, `publish/`) bzw. sollten es für
`artifacts/` ebenfalls sein (siehe unten). Sie wurden **nicht** in den
Branch committet und **nicht** auf GitHub hochgeladen.

## Signierungs- und Notarisierungsstatus

- **Kein Developer-ID-Zertifikat vorhanden.** Der neue CI-Job signiert nur
  Ad-hoc (`codesign --force --deep --sign -`).
- **Ad-hoc-Signierung ersetzt keine Developer-ID-Signierung und keine
  Apple-Notarisierung.** Nutzer anderer Macs werden weiterhin eine
  Gatekeeper-Warnung „nicht verifizierter Entwickler" sehen, bis echte
  Signierung + Notarisierung eingerichtet sind.
- Der Workflow enthält bereits auskommentierte, einsatzbereite Schritte für
  Developer-ID-Import (`security import` aus einem base64-codierten `.p12`)
  und Notarisierung (`xcrun notarytool submit` + `xcrun stapler staple`),
  gesteuert über die GitHub Secrets `MACOS_CERTIFICATE_P12_BASE64`,
  `MACOS_CERTIFICATE_PASSWORD`, `MACOS_SIGNING_IDENTITY`,
  `MACOS_NOTARIZATION_APPLE_ID`, `MACOS_NOTARIZATION_TEAM_ID`,
  `MACOS_NOTARIZATION_PASSWORD`. Sobald diese Secrets im Repository
  hinterlegt sind, können die Kommentarblöcke aktiviert werden.

## Nächste Schritte (nicht Teil dieser Sitzung)

1. `scripts/verify-macos-app-bundle.sh` auf einem echten Mac ausführen und
   die App per Finder-Doppelklick sowie `open` testen.
2. Erst danach: Branch pushen (unten bereits erledigt) und ggf. per Pull
   Request nach `main` mergen.
3. Erst nach erfolgreichem manuellem Test auf dem Mac: `release-v1.6.2.yml`
   manuell auslösen (`workflow_dispatch`) oder auf `main` mergen, damit der
   neue `macos-packages`-Job die echten macOS-ZIPs für das bestehende
   `v1.6.2`-Release nachträgt (`gh release upload ... --clobber` via
   bestehendem `release`-Job).
4. Developer-ID-Zertifikat + Notarisierungs-Secrets besorgen und die
   auskommentierten Workflow-Schritte aktivieren.
