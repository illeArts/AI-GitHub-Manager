# macOS-Release-Abnahme — AI GitHub Manager v1.6.3

## Ergebnis

**macOS releasefertig, ad-hoc signiert, nicht notarisiert.**

Die macOS-Artefakte sind technisch vollständig geprüft. Es gibt keine
Developer-ID- oder Notarisierungs-Secrets im GitHub-Repository; eine
Developer-ID-Signierung oder Notarisierung wird deshalb ausdrücklich nicht
behauptet. `spctl` darf die ad-hoc-signierte App auf einem fremden Mac beim
ersten Start ablehnen.

## Abgenommener Stand

- Abgenommener Build-Commit: `c894ce5` auf
  `feature/v1.6.3-understandable-git-ui`.
- .NET SDK: 8.0.129 (Apple Silicon).
- `dotnet build -c Release`: 0 Warnungen, 0 Fehler.
- `dotnet test -c Release`: 249/249 bestanden.

## Bundles und Archive

`bash scripts/verify-macos-app-bundle.sh` hat für beide RIDs erfolgreich
ausgeführt: self-contained Publish, echtes `.app`-Bundle, Architekturprüfung,
`plutil -lint`, Bereinigung code-signing-blockierender XAttrs, ad-hoc
`codesign --verify --deep --strict`, ZIP-Erstellung, Entpacken und erneute
Codesign-Prüfung.

| Artefakt | SHA-256 |
| --- | --- |
| `AI-GitHub-Manager-v1.6.3-macos-arm64.zip` | `645f5722c1f727746284813909874095b1f7a61cc3c0654496213741d17c474c` |
| `AI-GitHub-Manager-v1.6.3-macos-x64.zip` | `11e1f90957d54ff1f82982fc922ad7c7c919c31eacfd5e945ec43fca2145f7a2` |

Die gleichnamigen `.sha256`-Dateien liegen neben den ZIPs und wurden mit
`shasum -a 256 -c` geprüft. Die lokalen Abnahme-Artefakte sind:

```text
artifacts/appbundle/osx-arm64/AI GitHub Manager.app
artifacts/appbundle/osx-x64/AI GitHub Manager.app
artifacts/dist/AI-GitHub-Manager-v1.6.3-macos-arm64.zip
artifacts/dist/AI-GitHub-Manager-v1.6.3-macos-x64.zip
```

## Reeller Apple-Silicon-Test

Die arm64-App wurde aus dem `.app`-Bundle gestartet. macOS Accessibility
bestätigte die Menüleiste `AI GitHub Manager` und `Hilfe`. Das App-Menü
enthielt Über, Einstellungen, Dienste, Ausblenden und Beenden; das Hilfe-Menü
enthielt Hilfe öffnen, GitHub-Projekt und Release Notes. Über und Einstellungen
wurden aus dem nativen Menü geöffnet. Der Hauptbereich einschließlich der
kompakten lokalen Verknüpfung, gruppierten Aktionen und Ausgabe-/Fehleranalyse
startete ohne sichtbaren Fehler. Die in Einstellungen gespeicherte DE/EN-
Umschaltung bleibt über `AppSettingsService` persistent; Über, Einstellungen
und die Inhalte des Hilfe-Menüs wechselten im Live-Test auf Englisch und nach
dem Rückwechsel wieder auf Deutsch. Avalonia Native aktualisiert den bereits
exportierten macOS-Root-Titel `Hilfe` nicht live; der Menüinhalt wird dennoch
korrekt lokalisiert. Der Test endete mit der gespeicherten Sprache Deutsch.

## Zentraler späterer Release

Der zentrale Workflow `release-v1.6.3.yml` baut `osx-arm64` und `osx-x64` als
Matrix, legt jeweils ZIP und SHA-256-Datei als `macos-arm64` bzw. `macos-x64`
Actions Artifact ab und lädt im einzigen finalen Release-Job alle Artefakte
zusammen hoch. Es gibt keinen separaten macOS-Upload in einen möglicherweise
noch nicht existierenden Release. Die XAttr-Prüfung behandelt
`com.apple.provenance` auf macOS 26 korrekt als harmlose Metadaten, während
FinderInfo und ResourceFork weiterhin hart fehlschlagen.

Die Assetnamen entsprechen `ReleaseAssetSelector`:

- `AI-GitHub-Manager-v1.6.3-macos-arm64.zip`
- `AI-GitHub-Manager-v1.6.3-macos-x64.zip`

## Bekannte Einschränkung / nächster Schritt

Für echte Notarisierung werden später ein base64-kodiertes Developer-ID-P12,
Zertifikatspasswort, Apple Team ID sowie Apple-ID plus app-spezifisches Passwort
oder ein App-Store-Connect-API-Key benötigt. Bis dahin gilt der Gatekeeper-
Hinweis in den Release Notes (Finder: Rechtsklick → Öffnen; alternativ der
dort ausdrücklich als Beispiel markierte `xattr`-Befehl).

Auf dem Windows-PC bleiben nur Windows/Linux-Abnahme und die zentrale,
gemeinsame Release-Veröffentlichung offen. Der Release wurde im Rahmen dieser
Abnahme nicht ausgelöst.
