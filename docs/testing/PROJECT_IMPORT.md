# Prüfnachweis: Projektimport (Issue #13)

Datum: 2026-10-28. Basis `main` bis `18f0b44`, Feature-Branch `codex/13-safe-project-import`. Maßgeblich für die endgültige Source-Revision sind PR-Head und zugehöriger CI-Lauf. Keine Freigabe oder Auslieferung einer neuen Version durch diesen Bericht.

| Prüfung | Ausgeführt / Ergebnis | Aussagegrenze |
| --- | --- | --- |
| Release-Build der ganzen Lösung | Windows, SDK 10.0.403, Ziel net8.0: PASS, 0 Fehler, 0 Warnungen | kein Installer-/Signierungsnachweis |
| Automatisierte Suite | Windows: PASS, 316/316, 0 übersprungen | gh-Metadaten/Fehler injiziert; lokale Git-Vorgänge real |
| Echter privater Import | PASS über Produktionsdienste und vorhandenes gh-Login | Remote-Inhalt nur gelesen; kein Test-Push auf ein echtes privates Repo |
| SSH-origin nach Import | PASS: Safe Pull mit prozessbezogenem gh-HTTPS-Helper, gespeicherte SSH-origin unverändert | lokaler Testclone; kein Remote-Schreibvorgang |
| Globale Git-Konfiguration | PASS: im Live-Smoke bytegleich vor/nach Import und Pull | externe, zeitgleiche Konfigurationsänderungen würden Prüfung fehlschlagen lassen |
| Windows-Oberfläche | neue Aktionenbeschriftungen und Import-Zieldialog am lokalen Release beobachtet | UI-Smoke, kein vollständiger automatisierter UI-End-to-End-Test |
| DE/EN | PASS: eingebettete Kataloge, Schlüssel-/Formatparität, alle Fehler/Phasen, Fallback | Sprachwahl bleibt bestehende Produktfunktion |
| Windows/macOS CI | neue Matrix in `.github/workflows/build-test.yml`; Ergebnis am PR prüfen | macOS-Finder/native-Menü/Installer separat manuell abnehmen |
| Git-Diff | auf Featureumfang und vorhandene Aktionen geprüft; Whitespace mit CRLF-Berücksichtigung geprüft | bestehende CRLF-Dateien erhalten |

## Abgedeckte Importfälle

Echter Clone in fehlendes/leeres Ziel; direkter Zielpfad; Status; Pull; Commit/Push gegen lokalen Bare-Remote einschließlich erstem Commit eines leeren Repositories; Link ohne Fetch/Datei-/Config-Änderung; ausdrücklicher Fast-Forward; Ablehnung von dirty/ignoriert/divergent/detached/fehlendem Upstream; Git-Lock und unterbrochener Merge; Fremdrepo und belegter Nicht-Repo-Ordner mit Bytevergleich; Windows-Junction bzw. Symlink; verschachtelte/externe Git-Verzeichnisse; Belegung während Clone; Änderung während Entscheidung; Login-/Remote-/Git-/Clone-/Rechtefehler; Abbruch und Kindprozessbeendigung; atomare Speicherung und ViewModel-Rollback auf Speicherfehler.

## Reproduktion

```sh
dotnet build AI.GitHubManager.sln -c Release
dotnet test AI.GitHubManager.sln -c Release --no-build
git -c core.whitespace=trailing-space,space-before-tab,cr-at-eol diff --check origin/main
```

Optionaler echter GitHub-Test, ausschließlich mit einem Repository, auf das die aktuelle gh-Anmeldung zugreifen darf:

```sh
dotnet run --project tools/ImportSmoke -c Release -- https://github.com/OWNER/REPOSITORY.git
```

Der Smoke-Test klont in einen eindeutigen Testordner unter dem System-Temp-Verzeichnis, prüft Zuordnung/Status/Link/Pull und entfernt ausschließlich diesen eigenen Ordner auf Erfolg. Bei Fehler bleibt er zur Untersuchung erhalten. Es gibt keinen Push und keine Änderung an Remote-Repository-Inhalten. Die URL darf keine Zugangsdaten enthalten. Dieser Test wird nicht automatisch von der normalen Testsuite oder CI gestartet.

## Während der Entwicklung behoben

- Kulturabhängige Einbettung der JSON-Ressourcen korrigiert (`WithCulture=false`); durch Lokalisierungstest entdeckt.
- Abbruch beendet den tatsächlichen Git-Prozessbaum statt nur das .NET-Process-Objekt freizugeben.
- Credential-Konfiguration von globalem `gh auth setup-git` auf Prozess-Scope umgestellt. Der Live-Test hatte bei parallelen Tests kurzzeitige Lesefehler auf der globalen `.gitconfig` sichtbar gemacht; mit Prozess-Scope bestehen Suite und Live-Test.
- Aktuellen main-Stand samt Aktionen-Seitenleiste, Diagnoseexport, Git-Sperren und macOS-Menüfix übernommen. Keine ältere UI über neuere Funktionen gelegt.
- Erster Push eines leeren Repository-Clones setzt den Upstream.
- Lange neue Listenbeschriftung umgebrochen, Pfadfeld für Accessibility benannt, Escape-Abbruch ergänzt.

Bekannte Grenzen stehen im [Sicherheitsvertrag](../PROJECT_IMPORT.md). Ein PASS der Testsuite ist keine manuelle Produktabnahme auf allen Plattformen.
