# AI GitHub Manager

AI GitHub Manager ist eine plattformübergreifende Avalonia/.NET-App für Windows, macOS und Linux. Ziel ist ein einfaches, sicheres Werkzeug für GitHub-Projekte: anmelden, Repository auswählen, lokalen Ordner verknüpfen und Pull/Commit/Push per Button ausführen.

## Warum dieses Projekt existiert

Viele GitHub-Probleme entstehen nicht durch Git selbst, sondern durch wechselnde Geräte, falsche Credential Stores, falsche Commit-E-Mails, neue Tokens, fehlende Workflow-Rechte und unterschiedliche lokale Projektpfade. Dieses Tool soll diese Fehler bündeln, prüfen und verständlich reparieren.

## Grundregel

Die App speichert keine GitHub-Tokens selbst.

Stattdessen nutzt sie:

```bash
gh auth login --scopes repo,workflow
gh auth setup-git
gh auth refresh --scopes repo,workflow
```

Damit liegen Credentials im sicheren Speicher des Betriebssystems bzw. in der GitHub-CLI-Verwaltung.

## Projektstruktur

```text
AI.GitHubManager.sln
├── src/AI.GitHubManager.App      # Avalonia Desktop UI
├── src/AI.GitHubManager.Core     # Git, GitHub CLI, Diagnose, Sync-Logik
├── src/AI.GitHubManager.Data     # Lokaler ProjectStore, aktuell JSON
└── tests/AI.GitHubManager.Tests  # Platzhalter für spätere Tests
```

## Voraussetzungen

- .NET 8 SDK
- Git
- GitHub CLI `gh`
- Visual Studio 2022 oder JetBrains Rider oder VS Code mit C# Dev Kit

## Start in Visual Studio

1. `AI.GitHubManager.sln` öffnen.
2. NuGet-Pakete wiederherstellen lassen.
3. Startprojekt: `AI.GitHubManager.App`.
4. Ausführen.

## Erster Login

Im Terminal einmal ausführen:

```bash
gh auth login --scopes repo,workflow
gh auth setup-git
```

Wenn Push auf `.github/workflows/*.yml` blockiert wird:

```bash
gh auth refresh --scopes repo,workflow
```

## Aktueller Funktionsstand

MVP-Gerüst ist vorbereitet:

- Avalonia-Fenster
- Projektliste
- lokaler Pfad je Plattform speicherbar
- JSON-Projektstore
- Git-Status
- Pull
- Commit + Push
- GitHub-CLI-Statusprüfung
- Workflow-Scope-Reparatur
- Git-Credential-Setup über `gh auth setup-git`

## Nächste Aufgaben für mitwirkende KIs

Bitte in dieser Reihenfolge weiterarbeiten:

### 1. Projektimport verbessern

- `gh repo list --json ...` auslesen
- Repositories in der UI anzeigen
- Repo per Button als ManagedProject übernehmen
- Remote-URL automatisch setzen

### 2. Ordnerauswahl einbauen

- Avalonia StorageProvider verwenden
- Button `Ordner wählen`
- ausgewählten Pfad direkt als WindowsPath/MacPath/LinuxPath speichern

### 3. Sync-Engine ergänzen

Vor jedem Push prüfen:

- Ist Git installiert?
- Ist gh installiert?
- Ist User authentifiziert?
- Ist lokaler Ordner ein Git-Repository?
- Stimmt `origin` mit GitHub-Repo überein?
- Welche Branch ist aktiv?
- Gibt es uncommitted changes?
- Gibt es Konflikte?
- Gibt es Workflow-Dateien?
- Ist `workflow`-Scope vorhanden?

### 4. Fehleranalyse nutzerfreundlich machen

Häufige Fehler erkennen und erklären:

- `Authentication failed`
- `repository not found`
- `refusing to allow a Personal Access Token to create or update workflow`
- `non-fast-forward`
- `unrelated histories`
- `merge conflict`
- `nothing to commit`

### 5. UI erweitern

Ziel-Layout:

- Dashboard
- GitHub-Repositories
- Lokale Projekte
- Projekt-Detail
- Sync-Protokoll
- Einstellungen

### 6. Tests hinzufügen

Mindestens testen:

- Fehlertext-Parser
- ProjectStore Load/Save
- Plattformpfad-Erkennung
- CommandResult-Auswertung

## Sicherheitsregeln

- Niemals Tokens in JSON, Logs oder Settings speichern.
- Niemals automatisch `git push --force` ausführen.
- Niemals Dateien vor einem Pull löschen.
- Bei Konflikten abbrechen und verständlich melden.
- Vor riskanten Aktionen später Backup/Checkpoint einbauen.

## Namensraum

Alle Projekte verwenden:

```text
AI.GitHubManager
```

## Produktziel

AI GitHub Manager soll kein AAIA-only Tool sein. Es ist ein allgemeiner GitHub-Manager für alle aktuellen und zukünftigen Projekte.

## Update 2026-06-05: GitHub Login Button

Die App besitzt jetzt eigene Buttons für:

- `GitHub CLI installieren`
- `GitHub Login`
- `Umgebung prüfen`
- `GitHub Rechte: repo + workflow`
- `Git Credentials reparieren`

Wichtig: Die App speichert absichtlich keine GitHub-Tokens. Der Login läuft über die offizielle GitHub CLI (`gh`) und den sicheren Credential Store des Betriebssystems.

### Empfohlene Reihenfolge auf Windows

1. App starten.
2. `GitHub CLI installieren` drücken.
3. Nach der Installation Visual Studio/App neu starten.
4. `GitHub Login` drücken.
5. Im geöffneten Terminal den Browser-Code bestätigen.
6. `Umgebung prüfen` drücken.
7. Bei Workflow-Fehlern `GitHub Rechte: repo + workflow` drücken.
8. Danach `Git Credentials reparieren` drücken.

### Manuell im Terminal

Windows:

```powershell
winget install --id GitHub.cli --source winget
gh auth login --web --scopes repo,workflow
gh auth setup-git
```

macOS:

```bash
brew install gh
gh auth login --web --scopes repo,workflow
gh auth setup-git
```

Linux:

```bash
# GitHub CLI je nach Distribution installieren
gh auth login --web --scopes repo,workflow
gh auth setup-git
```

### Warum kein Token-Feld?

Token-Felder erzeugen genau das Chaos, das dieses Programm verhindern soll: falscher Token, falsche E-Mail, falsche Rechte, falscher Credential Store. Darum nutzt dieses Projekt `gh` als offizielle Login-Schicht.
