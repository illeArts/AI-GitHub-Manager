# AI GitHub Manager v1.6.2

AI GitHub Manager 1.6.2 behebt drei Produktivfehler: plattformfremde Update-Downloads, Datenverlust-Risiko bei Pull mit lokalen Änderungen (Issue #4), und einen real reproduzierten `index.lock`-Fehler bei parallelen Git-Operationen.

## Empfohlener Download

### Windows 64-Bit

Für die normale Installation verwenden:

`AI_GitHub_Manager_Setup_1.6.2_win-x64.exe`

Das portable ZIP-Paket bleibt zusätzlich verfügbar und benötigt keine Installation.

Der Windows-Installer ist derzeit nicht mit einem kostenpflichtigen Code-Signing-Zertifikat signiert. Windows SmartScreen kann deshalb beim ersten Start einen Warnhinweis anzeigen. Die zugehörige `.sha256`-Datei ermöglicht die Prüfung, ob der Download unverändert ist.

### macOS und Linux

- macOS Intel: `AI-GitHub-Manager-v1.6.2-macos-x64.zip`
- macOS Apple Silicon: `AI-GitHub-Manager-v1.6.2-macos-arm64.zip`
- Linux x64: `AI-GitHub-Manager-v1.6.2-linux-x64.tar.gz`

## Neu in 1.6.2

### 1. Plattformspezifische Update-Auswahl

Bisher verlinkte der Update-Check auf allen Betriebssystemen fest auf die Windows-`.exe`. Auf macOS oder Linux wurde also ein falsches, nicht ausführbares Paket angeboten.

- Die Plattform- und Architekturerkennung erfolgt jetzt ausschließlich über .NET-APIs
  (`OperatingSystem.IsWindows()/IsMacOS()/IsLinux()`, `RuntimeInformation.ProcessArchitecture`) —
  nicht mehr über Zeichenketten-Raten.
- Neue, dedizierte Klassen `PlatformDescriptor` (Plattformerkennung) und `ReleaseAssetSelector`
  (Asset-Auswahl) trennen diese Logik sauber vom `UpdateCheckService` und vom ViewModel.
- Zuordnung Plattform → Release-Asset:
  - Windows x64 → `AI_GitHub_Manager_Setup_<version>_win-x64.exe`
  - macOS Intel x64 → `AI-GitHub-Manager-v<version>-macos-x64.zip`
  - macOS Apple Silicon (arm64) → `AI-GitHub-Manager-v<version>-macos-arm64.zip`
  - Linux x64 → `AI-GitHub-Manager-v<version>-linux-x64.tar.gz`
- **Es wird nie mehr ein Windows-Download auf macOS oder Linux angeboten** (und umgekehrt kein
  falsches Paket für eine andere Architektur).
- SHA256-Prüfsummendateien werden nie als Programm-Asset ausgewählt.
- Gibt es für die laufende Plattform/Architektur kein passendes Paket im aktuellen Release
  (z. B. Linux Arm64), wird **kein** Download gestartet. Stattdessen öffnet die App die Release-Seite
  und zeigt eine verständliche Meldung, z. B.: „Für macOS Apple Silicon ist in diesem Release derzeit
  kein passendes Paket verfügbar. Die Release-Seite wurde geöffnet.“
- Die Oberfläche zeigt den tatsächlich gewählten Dateinamen des Update-Pakets an.

### 2. Sicherer Pull mit automatischer Schutzsicherung

Bisher schlug ein Pull bei lokalen Änderungen einfach mit der rohen Git-Fehlermeldung fehl; die App
forderte nur auf, selbst zu committen oder zu stashen.

- Vor jedem Pull prüft die neue `SafePullService`-Klasse den Arbeitsbaum (`git status --porcelain`).
  - Sauberer Arbeitsbaum → direkter `git pull --ff-only`.
  - Lokale Änderungen vorhanden → automatische, eindeutig gekennzeichnete Schutzsicherung per
    `git stash push --include-untracked --message "AI GitHub Manager auto-backup <UTC-Zeitstempel> <ID>"`,
    danach `git pull --ff-only` (**nie** ein automatischer Merge oder Rebase).
  - Nach erfolgreichem Pull wird die Sicherung kontrolliert mit `git stash apply` (nie `stash pop`)
    wiederhergestellt und erst nach verifiziert konfliktfreier Wiederherstellung gelöscht.
  - Schlägt der Pull fehl, wird der ursprüngliche Zustand aus der Sicherung vollständig
    wiederhergestellt, bevor der ursprüngliche Pull-Fehler angezeigt wird. Es werden keine Dateien
    verworfen und kein `git reset --hard` ausgeführt.
- **Konfliktfall (fail-closed):** Erzeugt die Wiederherstellung der Sicherung Konflikte, stoppt die
  App sofort. Die Sicherung wird **nicht** gelöscht, die betroffenen Dateien werden angezeigt, und es
  wird klar kommuniziert, dass Remote-Stand und lokale Änderungen nicht automatisch zusammengeführt
  werden konnten. Es erfolgt keine weitere automatische Schreiboperation. Verfügbare Aktionen:
  „Konflikte anzeigen“, „Sicherung behalten“, „Wiederherstellung erneut versuchen“ — keine Aktion
  verwirft lokale Änderungen ungefragt.
- Die exakte Stash-Referenz wird nach der Erstellung anhand der eindeutigen Kennung in
  `git stash list` ermittelt statt blind `stash@{0}` anzunehmen. Bestehende, eigene Stashes des
  Nutzers werden dabei nie verändert oder gelöscht.
- Neue Einstellung **„Sicherer Pull mit automatischer Schutzsicherung“** (Standard: aktiviert).
  Alternative: **„Bei lokalen Änderungen nur warnen und Pull abbrechen“** — dabei wird ebenfalls keine
  automatische Sicherung erstellt und nichts verändert.
- Klar unterscheidbare Ergebniszustände (`CleanPullSucceeded`, `BackupCreated`,
  `PullFailedAndRestored`, `PullSucceededAndChangesRestored`, `RestoreConflict`,
  `BackupCreationFailed`, `RestoreFailed`, `FastForwardNotPossible`) werden strukturiert an die
  Oberfläche gemeldet statt nur als Konsolentext.

### 3. Sichere Behandlung von `.git/index.lock`

Real reproduzierter Fehler, den dieses Update behebt:

```text
error: Unable to create '.git/index.lock': File exists.
Another git process seems to be running in this repository, or the lock file may be stale.
```

- **Serialisierung pro Repository:** Pro normalisiertem Repository-Pfad läuft nie mehr als eine
  schreibende Git-Aktion gleichzeitig (Pull, Commit + Push, Build/Test/Push) — intern über eine
  `SemaphoreSlim`-Sperre (`RepositoryLockService`), gemeinsam genutzt von allen internen Git-Diensten.
- **`.git/index.lock` wird niemals blind gelöscht.** Vor jeder schreibenden Aktion prüft
  `GitLockGuard`, ob noch ein aktiver Git-Prozess läuft (unter Windows per WMI-Abfrage auf
  `Win32_Process.CommandLine`, mit sicherem Fallback auf „irgendein Git-Prozess läuft“, falls die
  WMI-Abfrage fehlschlägt oder nicht eindeutig einem Repository zugeordnet werden kann). Läuft ein
  Prozess, bricht die Aktion verständlich ab — die Sperre bleibt unangetastet.
- Nur wenn kein aktiver Prozess erkannt wurde **und** die Sperrdatei alt genug ist, gilt sie als
  verwaist. Direkt vor dem Löschen wird das erneut geprüft (schließt die Race zwischen Prüfung und
  Aktion). Nach dem Entfernen läuft `git status` — nur bei gültigem Ergebnis gilt die Reparatur als
  erfolgreich.
- **Unterbrochene Git-Zustände werden strukturiert erkannt und nie automatisch bereinigt:**
  `MERGE_HEAD`, `CHERRY_PICK_HEAD`, ein laufender Rebase (`REBASE_HEAD`/`rebase-merge`/`rebase-apply`)
  und `BISECT_LOG`. Die App meldet diese Zustände verständlich und verweist auf die passende manuelle
  Abbruch-/Abschluss-Aktion (z. B. `git merge --abort`).
- Neuer, nur bei Bedarf sichtbarer Button **„Verwaiste Git-Sperre sicher entfernen“** für den seltenen
  Fall, dass eine Sperre nach einem abgestürzten externen Prozess zurückbleibt.
- Bestehende, fremde Benutzer-Stashes und lokale Änderungen bleiben in jedem Fall unverändert; es wird
  weiterhin nie `git reset --hard` verwendet.

## Sicherheit

- Kein automatischer `git reset --hard`.
- Keine automatische Löschung lokaler Dateien.
- Kein Force-Push.
- Kein blindes `stash pop`.
- Kein Zugriff auf beliebige, bestehende Benutzer-Stashes.
- Ungetrackte Dateien werden bei der Schutzsicherung mitgesichert.
- Remote-URLs werden vor jeder Protokollierung/Anzeige von Zugangsdaten bereinigt.
- Bei Unsicherheit bricht die App ab, statt optimistisch fortzufahren.

## Downloads und Integrität

Das Release veröffentlicht reproduzierbar erzeugte Pakete für:

- Windows x64 als Installer und portables ZIP
- macOS x64
- macOS Apple Silicon (arm64)
- Linux x64

Alle veröffentlichten Programmdateien erhalten eine separate SHA-256-Prüfsumme.

## Voraussetzungen

- Für GitHub-Funktionen: Git und GitHub CLI (`gh`).
- Für die Windows-Installer-Erstellung innerhalb der App: Inno Setup 6.
- Für Entwicklung aus dem Quellcode: .NET 8 SDK.

## Lizenz

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Download, Installation und eigene Nutzung sind gemäß der Datei `LICENSE` gestattet. Verkauf, fremde Binärdistributionen, kommerzielle Dienste und öffentliche Neuveröffentlichungen veränderter Versionen benötigen eine vorherige schriftliche Genehmigung.

## Hinweis

AI GitHub Manager ist ein unabhängiges Projekt und kein offizielles Produkt von GitHub, Inc. oder Microsoft Corporation.
