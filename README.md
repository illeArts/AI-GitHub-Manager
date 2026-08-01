# AI GitHub Manager

<p align="center">
  <img src="Logo/AI-GitHub_Manager.png" alt="AI GitHub Manager Logo" width="220" />
</p>

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
└── tests/AI.GitHubManager.Tests  # Unit- und Integrationstests
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
- plattformübergreifender Dialog „Projekt exportieren / Clean Export“
- unveränderlicher Exportplan mit vier Profilen, Vorschau, Secret-Warnung und großen Dateien
- sichere ZIP-Erstellung und Inhaltsvalidierung ohne Shell-Aufrufe
- Git-Worktree-Erkennung mit ausdrücklichem Synchronisierungs- und Backup-Hinweis
- strukturierte Authentifizierungsdiagnose (unterscheidet Keyring-, Umgebungstoken- und Mischzustände)
- Ein-Klick-Reparatur für einen ungültigen GH_TOKEN/GITHUB_TOKEN, der eine gültige Anmeldung blockiert
- Remote-URL-Normalisierung, Erkennung von Zugangsdaten/Platzhaltern in der URL, Ein-Klick-Bereinigung
- redigierter Diagnosebericht-Export ohne Tokens/Passwörter
- vollständig zweisprachiges UI (Deutsch/Englisch) inkl. Hilfe-Fenster, Über-Fenster und Export-Dialog
- Self-Service „Build, Test & Push" für .NET-Projekte (bricht vor dem Push ab, wenn Build oder Test fehlschlagen)
- „Installer erstellen" für Windows (Publish + Inno Setup) und macOS (build-installer-mac.sh)

Das vollständige Benutzerhandbuch (Schnellstart, alle Befehle, Fehlerbehebung, Sicherheit) ist im
Menü **Hilfe → Hilfe / Befehle** der App verfügbar und existiert auf Deutsch und Englisch.

## Nächste Aufgaben für mitwirkende KIs

Projektimport, Ordnerauswahl, Sync-Preflight, verständliche Git-Fehleranalyse und eine automatisierte Unit-/Integrationstestsuite sind umgesetzt. Sinnvolle nächste Schritte sind:

- Clean Export auf den unterstützten macOS- und Linux-Zielsystemen manuell verifizieren
- Bedienoberfläche und Lokalisierung des Exportdialogs weiter vereinheitlichen
- Release-Artefakte für Windows und macOS automatisiert erstellen und prüfen
- zusätzliche Regressionstests ergänzen, wenn neue Randfälle bekannt werden

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

## Version 1.6.0 — Selbst-Build, Test & Installer-Erstellung; UI-Layout

Neu:

- **Build, Test & Push**: Für .NET-Projekte (z. B. dieses Repository) führt die App `dotnet build`,
  dann `dotnet test` aus. Nur bei Erfolg beider Schritte wird committet und gepusht — bei einem
  Fehlschlag wird nichts gepusht. Erkennt automatisch, ob ein unterstütztes .NET-Build-System
  (`.sln`/`.csproj`) im Projektordner vorhanden ist.
- **Installer erstellen**: Erstellt auf Windows einen Installer (Self-Contained-Publish + Inno Setup
  6, sofern installiert) und auf macOS über das vorhandene `build-installer-mac.sh`. Läuft direkt
  aus der App, ohne das lokale, nicht versionierte `build-installer-win.bat` aufzurufen (das Skript
  ist bewusst git-ignoriert und wartet interaktiv auf Tasteneingaben, was bei nicht-interaktiver
  Ausführung zum Einfrieren führen würde).
- **UI-Layout**: Die Aktions-Buttons standen bisher direkt unter der Projektliste und nahmen ihr
  fast den ganzen Platz weg. Neues drittes Panel rechts neben dem Ausgabefeld nimmt jetzt alle
  Aktions-Buttons auf; die Projektliste links hat wieder ausreichend Raum.

## Version 1.5.1 — Build-Fix für die Avalonia-Oberfläche

Der Release-Build von 1.5.0 schlug mit `AVLN2000: Button besitzt keine Eigenschaft TextWrapping`
fehl (`MainWindow.axaml`). `TextWrapping` ist keine Button-Eigenschaft in Avalonia — beide neuen
Buttons („Ungültigen Token entfernen und Anmeldung reparieren“, „Remote sicher bereinigen“)
verwenden jetzt korrekt einen `TextBlock` als Button-Inhalt. Keine funktionalen Änderungen
gegenüber 1.5.0, reiner Build-Fix.

## Version 1.5.0 — Selbstdiagnose & Selbstreparatur der GitHub-Authentifizierung

Referenzfall: Ein ungültiger `GITHUB_TOKEN`/`GH_TOKEN` in der Windows-Umgebung überschreibt eine
gültige, im GitHub-CLI-Keyring gespeicherte Anmeldung. Die App meldete das bisher fälschlich als
„Nicht bei GitHub eingeloggt“ und blockierte den Push, obwohl `gh auth status` mit bereinigter
Umgebung eine gültige Anmeldung zeigt.

Neu in 1.5.0:

- Strukturierte Authentifizierungsdiagnose mit eigenem Zustandsmodell (`AuthenticationState`):
  `Authenticated`, `AuthenticatedViaKeyring`, `AuthenticatedViaEnvironmentToken`,
  `InvalidEnvironmentToken`, `EnvironmentTokenOverridesValidKeyring`, `NotAuthenticated`,
  `MissingRequiredScopes`, `GitHubCliUnavailable`, `AuthenticationCheckFailed`. Der Exitcode von
  `gh auth status` wird nicht mehr blind übernommen — die Ausgabe wird strukturiert ausgewertet.
- Zweite Prüfung mit bereinigter Prozessumgebung (ohne `GH_TOKEN`/`GITHUB_TOKEN`), sobald die erste
  Prüfung fehlschlägt und einer der beiden Werte gesetzt ist. So erkennt die App eine gültige
  Keyring-Anmeldung, die durch einen ungültigen Token verdeckt wird.
- Ein-Klick-Reparatur „Ungültigen Token entfernen und Anmeldung reparieren“: entfernt nur die
  betroffene(n) Umgebungsvariable(n) aus Prozess- und Benutzerumgebung (`HKCU\Environment`),
  lässt Systemvariablen (`HKLM`) unangetastet, sendet `WM_SETTINGCHANGE`, prüft danach sofort
  erneut — ohne Neustart. Der GitHub-CLI-Keyring wird dabei nie verändert.
- Keine Tokens mehr in Remote-URLs: `RemoteUrlNormalizer` erkennt eingebettete Zugangsdaten und
  Platzhalter wie `DEIN_VORHANDENER_TOKEN`, vergleicht Remote-URLs semantisch (HTTPS/SSH,
  mit/ohne `.git`, Groß-/Kleinschreibung) und bietet „Remote sicher bereinigen“ zur kanonischen
  Form `https://github.com/<owner>/<repository>.git` an.
- Verständlicherer GitHub-Login-Assistent: erkennt automatisch, ob bereits eine gültige Anmeldung
  besteht, ob nur eine Berechtigung fehlt, oder ob eine echte Neuanmeldung nötig ist — ein Laie
  muss weder Umgebungsvariablen noch Tokens noch den Windows-Schlüsselspeicher kennen.
- Diagnosebericht-Export: redigierter Bericht (Version, OS, Git-/CLI-Version, Repository-Pfad,
  credential-freie Remote-URL, Branch, Auth-Status, Scopes) zum Weitergeben an Entwickler — niemals
  mit Tokens, Passwörtern oder rohen Umgebungsvariablen-Werten.
- Push wird nur noch blockiert, wenn eine zwingende Voraussetzung tatsächlich fehlt, und die
  Meldung nennt den genauen Grund plus ob eine automatische Reparatur verfügbar ist.

## Version 1.4.0 — Clean Export

Version 1.4.0 ergänzt den plattformübergreifenden Dialog **„Projekt exportieren / Clean Export“**. Die Minor-Version wurde erhöht, weil es sich um eine neue, rückwärtskompatible Funktion handelt.

„Vollständiges Dateiarchiv“ bezeichnet kein Git-Wiederherstellungsbackup. Git-Worktrees werden nicht über ZIP-Dateien zwischen Rechnern synchronisiert; für Rechnerwechsel bleiben Fetch, Pull, Commit und Push der verbindliche Weg. Der Export verändert den Quellordner niemals. Potenziell sensible Dateien werden in Version 1 ausschließlich anhand bekannter Namen, Pfade und Dateiendungen erkannt.

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

## Update 2026-06-12: v1.3.0 — Sync-Preflight, Fehleranalyse, Auto-Update-Check

### Neu in 1.3.0

**Sync-Preflight (`Umgebung prüfen`)**
Der Button "Umgebung prüfen" führt jetzt — wenn ein Projekt mit lokalem Pfad ausgewählt ist — alle 9 Vor-Push-Checks in einem Schritt aus:

- Git installiert?
- GitHub CLI installiert?
- GitHub eingeloggt?
- Lokaler Ordner ist ein Git-Repository?
- Remote `origin` stimmt mit dem Projekt überein?
- Aktiver Branch erkannt?
- Uncommitted Changes vorhanden? (Warnung, kein Abbruch)
- Merge-Konflikt aktiv (MERGE_HEAD)?
- Workflow-Dateien vorhanden → workflow-Scope geprüft?

Ergebnis erscheint mit ✅/⚠️/❌ strukturiert im Ausgabe-Fenster.

**Fehleranalyse bei Pull/Push**
Schlägt ein Pull oder Push fehl, erkennt die App jetzt automatisch bekannte Fehlermuster und zeigt Ursache + Lösung im Klartext:

| Erkannter Fehler | Lösungshinweis |
|---|---|
| Authentication failed | GitHub Login + Git Credentials reparieren |
| repository not found | Repo-Existenz und Zugriffsrechte prüfen |
| workflow-Scope fehlt | GitHub Rechte: repo + workflow |
| non-fast-forward | Erst Pull, dann Push |
| unrelated histories | --allow-unrelated-histories |
| merge conflict | Konflikte auflösen, dann committen |
| index.lock | Abgestürzten Git-Prozess bereinigen |
| Netzwerkfehler | Internetverbindung prüfen |

**Update-Check**
Die App prüft beim Start automatisch die GitHub Releases API auf neue Versionen. Wenn eine neue Version verfügbar ist, erscheint ein grünes Banner oben in der App mit einem Direktdownload-Button. Über den Button "Auf Updates prüfen" in der linken Leiste kann manuell geprüft werden.

Der Update-Check läuft im Hintergrund, blockiert die App nicht und schlägt still fehl bei fehlendem Netz.

### Installer / Update

Der Windows-Installer erkennt eine vorhandene 1.x-Installation automatisch und aktualisiert sie in-place — kein manuelles Deinstallieren nötig. Einfach das aktuelle `AI_GitHub_Manager_Setup_<Version>_win-x64.exe` aus `dist\` ausführen.
