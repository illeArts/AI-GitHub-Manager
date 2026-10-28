# Projektimport: Architektur und Sicherheitsvertrag

Referenz: [Issue #13](https://github.com/illeArts/AI-GitHub-Manager/issues/13), [Atlas #1](https://github.com/illeArts/Atlas-Blueprint/issues/1). Basis: `main` ab `18f0b44`; Ziel Windows/macOS, .NET 8/Avalonia. Keine neue Produktversion oder Auslieferung durch diesen Feature-Branch.

## Komponenten

- `ProjectImportService`: testbare Zustandsmaschine mit `IProjectImportCommands`, `ImportInspection`, ausdrücklicher Entscheidung und typisierten Fehlercodes. Nur `ImportResult` belegt einen abgeschlossenen Dateivorgang; erst nach erfolgreichem Speichern bestätigt die UI den Import.
- `ProjectImportCommands`/`CommandRunner`: ArgumentList statt Shell-Zusammensetzung, macOS-PATH-Ergänzung, entfernte Git-Umgebungsumleitungen, nichtinteraktive Credentials, keine optionalen Git-Index-Schreibvorgänge bei Prüfungen. Abbruch beendet Git samt Kindprozessen und wartet auf deren Ende.
- `GitHubCredentialScope`: nur für Netzwerkbefehle gesetzter `gh auth git-credential`-Helper mit zurückgesetzter Helper-Liste für github.com. SSH-GitHub-URLs werden ausschließlich im Kindprozess auf HTTPS umgeleitet. Weder `gh auth setup-git` noch globale/lokale Config-Schreibvorgänge sind Bestandteil eines Imports.
- `MainWindowViewModel`: hält ausgewähltes Projekt und bestätigten Zielpfad während des Vorgangs fest, meldet lokalisierte Phasen, erlaubt Abbruch, speichert auf Erfolg und setzt die In-Memory-Zuordnung auf Speicherfehler zurück. Für importierte Projekte verwenden die bestehenden Pull-/Push-Dienste den Credential-Scope; die bisherigen Dienste bleiben für andere Projekte erhalten.
- `ImportDialogs`: Zielpfad (auch noch nicht existierend), Bestätigung mit Repository/Ziel, Link-/Update-Entscheidung und Fehlerdialog mit alternativer Ordnerwahl. Keine voreingestellte destruktive Standardaktion; Escape bricht ab. Die bestehende Aktionen-Seitenleiste, Diagnoseexport und macOS-Anwendungs-/Hilfemenüs bleiben erhalten; macOS erhält zusätzlich Aktionen → Projekt im nativen Fenstermenü.
- `JsonProjectStore`: JSON im selben Verzeichnis temporär schreiben, anschließend per Dateiumbenennung ersetzen. Vorherige Datei bleibt bei Schreib-/Serialisierungsfehler erhalten. Fehlende neue JSON-Felder sind rückwärtskompatibel.

## Ablauf

1. Absolute Pfade normalisieren; Wurzelverzeichnisse, Dateien, Symlinks/Reparse Points in der gesamten Vorfahrenkette und Ziele innerhalb anderer Repositories blockieren. Gleichen Zielpfad über `RepositoryLockService.Shared` gegen andere schreibende App-Dienste serialisieren.
2. Git-Version, `gh auth status --hostname github.com`, `gh repo view owner/name --json id,url` prüfen. Repo-ID stammt aus der API und wird zusammen mit dem kanonischen Remote gespeichert. Adressen mit Zugangsdaten, fremden Hosts, Traversal oder Befehlsfragmenten werden vor einem Prozessaufruf abgewiesen.
3. Fehlendes/leeres Ziel: normaler, nicht flacher `git clone` in einen eindeutigen Nachbarordner auf demselben Volume. Keine `--depth`-Begrenzung. Prüfen, Abbruch prüfen, Ziel erneut prüfen. Ein noch leerer Zielordner darf ausschließlich nichtrekursiv entfernt werden; das schlägt bei zwischenzeitlich hinzugekommenen Dateien fehl. `Directory.Move` veröffentlicht den Clone ohne Ersetzen eines inzwischen existierenden Ziels.
4. Belegtes Ziel: nur eigenständiges Repository mit internem `.git`-Verzeichnis akzeptieren. Worktrees, externe Git-Verzeichnisse, Alternates und Reparse Points innerhalb der Git-Metadaten werden blockiert. `--show-toplevel`, origin, Branch, Upstream, Ahead/Behind, Status inkl. ignorierter Dateien sowie Sperr-/Unterbrechungsmarker prüfen.
5. **Link**: erneut prüfen, keine Projektdateien oder Git-Konfiguration ändern und nicht fetchen. **Update**: nur nach positiver Entscheidung; vor und nach Fetch erneut prüfen, `merge-base --is-ancestor HEAD @{u}` und ausschließlich `merge --ff-only --no-overwrite-ignore`, Autostash und Hooks ausgeschaltet. Dirty, voraus/divergent, detached, fehlender Upstream und aktive/unterbrochene Operationen blockieren Update, bleiben aber verknüpfbar.
6. Atomar speichern: bestehende Projekt-ID, GitHub-Repository-ID, Owner/Name/Web-URL, kanonisches Remote, absoluter Pfad ausschließlich für die aktuelle Plattform, UTC-Änderungszeit. Auswahlwechsel während des Vorgangs darf niemals die Zuordnung eines anderen Projekts ändern. Keine Erfolgszuordnung bei Abbruch oder Fehler; eine alte Zuordnung bleibt bestehen.

## Fehler und Geheimnisse

Core meldet `ImportError` statt roher Git-/gh-Ausgaben. UI-Texte einschließlich Fehlern, Fortschritt, Dialogen und Listenaktualisierung liegen in eingebetteten `Projects/Resources/Import.de.json` und `Import.en.json` (`WithCulture=false`). `ImportText` lädt weitere Sprachdateien automatisch und fällt pro fehlendem Schlüssel auf Englisch zurück. Eine neue Produktsprache muss zusätzlich im bestehenden Sprachwähler registriert werden. Tests prüfen Schlüsselparität, Formatargumente und alle Fehler-/Phasenschlüssel.

Abbruch/Fehler beendet den aktiven Prozess; kein rekursives Löschen von Importordnern und keine automatische Entfernung vermeintlich verwaister Nutzerdateien. Nach erfolgreichem Fast-Forward kann ein späterer Speicherfehler den bereits ausdrücklich autorisierten Update-Schritt nicht rückgängig machen. Stattdessen bleibt die vorige Zuordnung aktiv und die UI meldet den Speicherfehler.

## Grenzen

- Dateisystemprüfungen und atomare Veröffentlichung minimieren TOCTOU-Risiken. Andere Programme dürfen den Zielbaum während Import/Update nicht aktiv verändern. Gegen einen bösartigen Prozess mit denselben Dateirechten bieten portable .NET-Pfadprüfungen keine vollständige handlebasierte Isolation. Git verhindert beim Merge zusätzlich unbestätigtes Überschreiben von untracked/ignorierten Dateien.
- Git-Konfiguration, Credential Helper und installierte Git-/gh-Binaries sind lokale Vertrauensgrundlagen. Import führt keine Repository-Builds, Skripte oder Hooks aus. Bereits systemweit konfigurierte Checkout-Filter bleiben eine Git-Vertrauensgrundlage.
- Git-Submodule werden nicht rekursiv initialisiert. LFS-Objekte benötigen eine vorhandene Git-LFS-Installation und deren Zugriffsrechte. „Vollständig“ bezeichnet den normalen Git-Clone samt Historie und regulärem Checkout, keinen zusätzlichen Submodul-/LFS-Installer.
- Temporäre Clones können Platz belegen und private Inhalte enthalten; die UI weist auf ihre lokale Aufbewahrung hin. Keine ungefragte Bereinigung.
- Externe Worktrees und GitHub Enterprise sind in v1 bewusst blockiert; eine spätere Erweiterung benötigt einen gesonderten Sicherheitsvertrag.

## Prüfen

```sh
dotnet build AI.GitHubManager.sln -c Release
dotnet test AI.GitHubManager.sln -c Release --no-build
```

Die CI führt dieselben Prüfungen auf `windows-latest` und `macos-latest` aus. Authentifizierung wird im normalen Testlauf injiziert; Git-Operationen laufen gegen echte isolierte Bare-Repositories. Der separate Smoke-Test unter `tools/ImportSmoke` prüft auf ausdrücklichen Aufruf auch ein tatsächlich privates Repository mit den Produktionsdiensten; er pusht niemals.
