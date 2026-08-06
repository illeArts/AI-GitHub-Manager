# AI GitHub Manager

[![Version](https://img.shields.io/badge/version-1.6.4-blue)](#aktueller-funktionsstand)
[![Platforms](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#voraussetzungen)
[![License](https://img.shields.io/badge/license-proprietary%20community--use-orange)](LICENSE)

<p align="center">
  <img src="Logo/AI-GitHub_Manager.png" alt="AI GitHub Manager Logo" width="220" />
</p>

AI GitHub Manager ist eine kompakte, plattformübergreifende Avalonia/.NET-Desktopanwendung für Windows, macOS und Linux. Sie unterstützt Nutzer dabei, GitHub-Projekte auszuwählen, lokale Ordner korrekt zuzuordnen und typische Git-Arbeitsschritte wie Statusprüfung, Pull, Commit und Push verständlich auszuführen.

Das Projekt richtet sich insbesondere an Nutzer, die Git und GitHub zuverlässig verwenden möchten, ohne jede Credential-, Remote-, Branch- oder Workflow-Konfiguration manuell beherrschen zu müssen.

> **Wichtig:** Git-Operationen können Dateien, Branches, Commits und Repository-Zustände verändern. Prüfe vor jeder Aktion Remote, Branch, Änderungen und Backup. Die Nutzung erfolgt eigenverantwortlich und nach Maßgabe der [Lizenz](LICENSE).

## Warum dieses Projekt existiert

Viele GitHub-Probleme entstehen nicht durch Git selbst, sondern durch wechselnde Geräte, falsche Credential Stores, falsche Commit-E-Mails, neue Tokens, fehlende Workflow-Rechte und unterschiedliche lokale Projektpfade. AI GitHub Manager bündelt diese Prüfungen, erklärt bekannte Fehler verständlich und bietet kontrollierte Reparaturwege an.

## Sicherheitsprinzip

Die Anwendung speichert keine GitHub-Tokens selbst.

Stattdessen verwendet sie die offizielle GitHub CLI und den geschützten Credential Store des Betriebssystems:

```bash
gh auth login --scopes repo,workflow
gh auth setup-git
gh auth refresh --scopes repo,workflow
```

Weitere Grundregeln:

- kein automatischer `git push --force`;
- keine automatische Löschung von Dateien vor einem Pull;
- Abbruch und verständliche Meldung bei erkannten Konflikten;
- keine eigene Telemetrie oder automatische Übertragung von Projektdateien an illeArts;
- sensible Exporthinweise als Hilfestellung, nicht als Sicherheitsgarantie.

Siehe auch [SECURITY.md](SECURITY.md) und [PRIVACY.md](PRIVACY.md).

## Funktionen

- Avalonia-Desktopoberfläche für Windows, macOS und Linux
- Projektliste und plattformspezifische lokale Projektpfade
- lokaler JSON-Projektstore
- Git-Status, Pull sowie Commit und Push
- GitHub-CLI-Statusprüfung
- GitHub-Login über die offizielle CLI
- Reparatur der Git-Credential-Konfiguration
- Prüfung und Ergänzung des `repo`- und `workflow`-Scopes
- Sync-Preflight mit neun Vorabprüfungen
- verständliche Analyse häufiger Pull-/Push-Fehler
- plattformübergreifender Dialog „Projekt exportieren / Clean Export“
- vier unveränderliche Exportprofile mit Vorschau
- Warnungen vor bekannten Secret-Dateien und großen Dateien
- ZIP-Erstellung und Inhaltsvalidierung ohne Shell-Aufrufe
- Git-Worktree-Erkennung mit Synchronisierungs- und Backup-Hinweis
- automatische Prüfung der GitHub Releases API auf neue Versionen
- strukturierte Authentifizierungsdiagnose (unterscheidet Keyring-, Umgebungstoken- und Mischzustände)
- Ein-Klick-Reparatur für einen ungültigen GH_TOKEN/GITHUB_TOKEN, der eine gültige Anmeldung blockiert
- Remote-URL-Normalisierung, Erkennung von Zugangsdaten/Platzhaltern in der URL, Ein-Klick-Bereinigung
- redigierter Diagnosebericht-Export ohne Tokens/Passwörter
- vollständig zweisprachiges UI (Deutsch/Englisch) inkl. Hilfe-Fenster, Über-Fenster und Export-Dialog
- Self-Service „Build, Test & Push" für .NET-Projekte (bricht vor dem Push ab, wenn Build oder Test fehlschlagen)
- „Installer erstellen" für Windows (Publish + Inno Setup) und macOS (build-installer-mac.sh)
- vorsorglicher „Inno Setup installieren"-Button: erscheint unter Windows nur, solange Inno Setup 6
  nicht gefunden wurde, und verschwindet automatisch, sobald es installiert ist

## Sync-Preflight

Die Funktion **„Umgebung prüfen“** kontrolliert bei ausgewähltem Projekt:

1. Git installiert?
2. GitHub CLI installiert?
3. GitHub-Anmeldung vorhanden?
4. Lokaler Ordner ist ein Git-Repository?
5. Remote `origin` stimmt mit dem Projekt überein?
6. Aktiver Branch erkannt?
7. Uncommitted Changes vorhanden?
8. Merge-Konflikt aktiv?
9. Workflow-Dateien vorhanden und erforderlicher Workflow-Scope verfügbar?

Das Ergebnis wird strukturiert mit ✅, ⚠️ und ❌ angezeigt.

## Fehleranalyse

Bei fehlgeschlagenem Pull oder Push erkennt die Anwendung unter anderem folgende Muster:

| Erkannter Fehler | Angezeigter Lösungsweg |
|---|---|
| Authentication failed | GitHub-Login und Git-Credentials prüfen |
| Repository not found | Repository und Zugriffsrechte prüfen |
| Workflow-Scope fehlt | Rechte `repo` und `workflow` ergänzen |
| Non-fast-forward | Erst Pull, Konflikte prüfen, danach Push |
| Unrelated histories | Historien bewusst zusammenführen |
| Merge conflict | Konflikte manuell auflösen und committen |
| `index.lock` | abgestürzten Git-Prozess beziehungsweise Lock prüfen |
| Netzwerkfehler | Verbindung und Remote-Erreichbarkeit prüfen |

## Clean Export

Version 1.4.0 ergänzt den Dialog **„Projekt exportieren / Clean Export“**.

Ein „vollständiges Dateiarchiv“ ist kein vollständiges Git-Wiederherstellungsbackup. Git-Worktrees werden nicht über ZIP-Dateien zwischen Rechnern synchronisiert. Für den Rechnerwechsel bleiben Fetch, Pull, Commit und Push der verbindliche Weg.

Der Export verändert den Quellordner nicht. Potenziell sensible Dateien werden anhand bekannter Namen, Pfade und Dateiendungen erkannt. Diese Erkennung kann unvollständig sein; jeder Export muss vor einer Weitergabe manuell kontrolliert werden.

## Voraussetzungen

- .NET 8 SDK zum Entwickeln oder Bauen
- Git
- GitHub CLI `gh`
- Visual Studio 2022, JetBrains Rider oder VS Code mit C# Dev Kit

## Projektstruktur

```text
AI.GitHubManager.sln
├── src/AI.GitHubManager.App      # Avalonia Desktop UI
├── src/AI.GitHubManager.Core     # Git, GitHub CLI, Diagnose und Sync-Logik
├── src/AI.GitHubManager.Data     # lokaler ProjectStore, aktuell JSON
└── tests/AI.GitHubManager.Tests  # Unit- und Integrationstests
```

## Namensraum

Alle Projekte verwenden:

```text
AI.GitHubManager
```

## Produktziel

AI GitHub Manager soll kein AAIA-only Tool sein. Es ist ein allgemeiner GitHub-Manager für alle aktuellen und zukünftigen Projekte.

## Start aus dem Quellcode

1. Repository klonen.
2. `AI.GitHubManager.sln` öffnen.
3. NuGet-Pakete wiederherstellen.
4. `AI.GitHubManager.App` als Startprojekt festlegen.
5. Anwendung ausführen.

## GitHub-Anmeldung

### Über die Anwendung

Empfohlene Reihenfolge unter Windows:

1. Anwendung starten.
2. **GitHub CLI installieren** auswählen.
3. Nach der Installation Anwendung beziehungsweise Entwicklungsumgebung neu starten.
4. **GitHub Login** auswählen.
5. Browser-Code bestätigen.
6. **Umgebung prüfen** ausführen.
7. Bei Workflow-Fehlern **GitHub Rechte: repo + workflow** auswählen.
8. Anschließend bei Bedarf **Git Credentials reparieren** ausführen.

### Manuell

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
# GitHub CLI entsprechend der verwendeten Distribution installieren
gh auth login --web --scopes repo,workflow
gh auth setup-git
```

## Aktueller Funktionsstand

Aktuelle Projektversion: **1.6.4**

Das vollständige Benutzerhandbuch (Schnellstart, alle Befehle, Fehlerbehebung, Sicherheit) ist im
Menü **Hilfe → Hilfe / Befehle** der App verfügbar und existiert auf Deutsch und Englisch.

Noch offen (Stand 1.6.4, siehe RELEASE_NOTES_v1.6.4.md „Bekannte Einschränkungen“):

- Developer-ID-Signierung und Notarisierung für macOS (aktuell nur Ad-hoc-Signatur,
  Gatekeeper-Warnung bleibt bestehen);
- manueller End-to-End-Test des Windows-Installers auf einem echten
  Windows-System (Installation, Start, Kontextmenü, „Auf GitHub öffnen“);
- eigener Hilfe-Abschnitt zu den „Erweiterten Befehlen” (Rebase/Cherry-Pick/Force
  Push/Reset/Hard Reset/Clean) — die Bedienoberfläche selbst erklärt sie bereits
  vollständig im Bestätigungsdialog;
- vollständige Tastatur-/Screenreader-Durchgängigkeit über die gesamte Anwendung
  (bisher nur an den zentralen Bedienelementen und im neuen Projekt-Kontextmenü
  ergänzt);
- Oberfläche und Lokalisierung des Exportdialogs vereinheitlichen;
- Regressionstests bei neuen Randfällen erweitern.

## Änderungsprotokoll (Changelog)

### Version 1.6.4 — GitHub-Remote-Erkennung und Projekt-Kontextmenü

- Der GitHub-Link eines Projekts wird jetzt ausschließlich aus dem echten
  `git remote get-url origin` oder einer validierten manuellen Eingabe
  abgeleitet — nie aus dem angemeldeten GitHub-Account kombiniert mit dem
  lokalen Projektnamen. Organisations- und persönliche Repositories werden
  identisch behandelt.
- Neues Rechtsklick-Kontextmenü in der Projektliste: „Auf GitHub öffnen“,
  „GitHub-Link kopieren“, „GitHub-Link festlegen/bearbeiten …“, „Remote
  erneut erkennen“, „Projekt speichern“, „Aus Manager entfernen …“ (löscht
  nie den lokalen Ordner oder das GitHub-Repository).
- „Remote erneut erkennen“ fragt vor dem Überschreiben eines abweichenden,
  insbesondere manuell gesetzten Links explizit nach Bestätigung.
- Alte `projects.json`-Dateien laden unverändert weiter; neue Felder
  (`RepositoryWebUrl`, `RepositoryOwner`, `RepositoryName`, `RemoteSource`)
  erhalten sichere Standardwerte.
- Vollständige Details, Testergebnisse und bekannte Einschränkungen siehe
  [`RELEASE_NOTES_v1.6.4.md`](RELEASE_NOTES_v1.6.4.md).

### Version 1.6.3 — Verständliche Git-Bedienung, macOS-/Linux-Parität

- Git-Vorgänge sind jetzt als benanntes Dropdown mit 12 Einträgen, Risikostufen
  (Sicher / Vorsicht / Erweitert / Gefährlich), Erklärungsfeld (Geeignet für /
  Was wird verändert / Was bleibt unverändert) und sichtbarem technischem
  Befehl abgebildet, statt eines einzelnen vagen „Update"-Felds.
- Einstellungen, Hilfe/Benutzerhandbuch und Über-Dialog sind auf Windows,
  macOS und Linux gleichermaßen erreichbar; macOS erhält eine native
  Anwendungsmenüleiste.
- Erweiterte, potenziell gefährliche Befehle (Rebase, Cherry-Pick, Force Push,
  Reset, Hard Reset, Clean) verlangen einen expliziten Bestätigungsdialog;
  bei Hard Reset/Clean/Force Push zusätzlich die exakte Eingabe des
  Branch-Namens. Force Push nutzt ausschließlich `--force-with-lease`.
- Vollständige Details, Testergebnisse und bekannte Einschränkungen (u. a.
  macOS-Gatekeeper/Ad-hoc-Signierung ohne Notarisierung) siehe
  [`RELEASE_NOTES_v1.6.3.md`](RELEASE_NOTES_v1.6.3.md).

### Version 1.6.2 — Plattformspezifische Updates & sicherer Pull

- **Update-Check ist jetzt plattformbewusst**: Statt immer nach einer Windows-`.exe` zu suchen, wählt
  die App anhand der tatsächlichen Betriebssystem-/Architekturerkennung (`OperatingSystem.IsWindows()`
  /`IsMacOS()`/`IsLinux()`, `RuntimeInformation.ProcessArchitecture`) das passende Release-Asset:
  Windows x64, macOS Intel, macOS Apple Silicon oder Linux x64. Auf macOS/Linux wird nie mehr ein
  Windows-Installer angeboten. Gibt es für die aktuelle Plattform kein passendes Paket, wird kein
  falscher Download gestartet — stattdessen öffnet die App die Release-Seite und erklärt das klar.
  SHA256-Prüfsummendateien werden nie als Programm-Asset ausgewählt.
- **Sicherer Pull mit automatischer Schutzsicherung**: Ein Pull bei vorhandenen lokalen Änderungen
  bricht nicht mehr nur mit einer rohen Git-Fehlermeldung ab. Stattdessen sichert die App lokale
  Änderungen (inkl. neuer Dateien) automatisch per `git stash push --include-untracked` mit eindeutiger
  Kennung, führt `git pull --ff-only` aus (nie einen automatischen Merge/Rebase) und stellt die
  Sicherung danach kontrolliert per `git stash apply` wieder her. Schlägt der Pull fehl, wird der
  ursprüngliche Zustand vollständig wiederhergestellt. Erzeugt die Wiederherstellung Konflikte, stoppt
  die App fail-closed, behält die Sicherung und zeigt die betroffenen Dateien — es wird nie automatisch
  etwas verworfen oder ein bestehender, eigener Stash des Nutzers verändert. Neue Einstellung: „Sicherer
  Pull mit automatischer Schutzsicherung“ (Standard: an) versus „Bei lokalen Änderungen nur warnen und
  Pull abbrechen“.
- **Sichere Behandlung von `.git/index.lock`**: Reale Fehlermeldung, die dieses Update behebt:
  `error: Unable to create '.git/index.lock': File exists. Another git process seems to be running
  in this repository, or the lock file may be stale.` Pro Projektordner läuft jetzt nie mehr als eine
  schreibende Git-Aktion gleichzeitig (Pull, Commit + Push, Build/Test/Push) — intern über eine
  `SemaphoreSlim`-Sperre je normalisiertem Repository-Pfad. Vor jeder schreibenden Aktion wird zusätzlich
  geprüft, ob ein aktiver Git-Prozess erkannt werden kann (unter Windows per WMI-Abfrage, mit sicherem
  Fallback auf „irgendein Git-Prozess läuft“, falls das nicht möglich ist); erst wenn kein aktiver Prozess
  erkannt wurde und die Sperrdatei alt genug ist, gilt sie als verwaist und wird entfernt — direkt vor dem
  Löschen wird das erneut geprüft. Unterbrochene Zustände (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REBASE_HEAD`/
  laufender Rebase, `BISECT_LOG`) werden strukturiert erkannt und **nie** automatisch bereinigt. Nach dem
  Entfernen einer verwaisten Sperre prüft die App `git status`, bevor irgendetwas fortgesetzt wird. Neuer,
  nur bei Bedarf sichtbarer Button **„Verwaiste Git-Sperre sicher entfernen“**.
- Siehe RELEASE_NOTES_v1.6.2.md für Details.

### Version 1.6.1 — Vorsorglicher Inno-Setup-Hinweis

„Installer erstellen“ auf Windows benötigt Inno Setup 6 (kostenlos, https://jrsoftware.org/isinfo.php).
Bisher stand das nur im Fehlertext, falls die Erstellung deshalb fehlschlug. Neu:

- Das Hilfe-Fenster (Menü **Hilfe → Hilfe / Befehle**) erklärt jetzt explizit, dass Windows dafür
  Inno Setup 6 braucht und wo man es bekommt.
- Vorsorglicher Button **„Inno Setup installieren“**: Die App prüft beim ersten Start (und danach bei
  jeder Umgebungsprüfung sowie nach jedem Installer-Lauf) automatisch, ob Inno Setup 6 an einem der
  bekannten Installationsorte gefunden wird.
  - Gefunden → Button ist nicht sichtbar.
  - Nicht gefunden → Button erscheint neben „Installer erstellen“ und öffnet beim Klick die offizielle
    Download-Seite im Standardbrowser. Es wird nichts automatisch heruntergeladen oder installiert.
  - Nach einer manuellen Installation verschwindet der Button von selbst (sobald erneut geprüft wird),
    ganz ohne App-Neustart.
- Nur unter Windows relevant; auf macOS/Linux bleibt der Button dauerhaft ausgeblendet.

### Version 1.6.0 — Selbst-Build, Test & Installer-Erstellung; UI-Layout

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

### Version 1.5.1 — Build-Fix für die Avalonia-Oberfläche

Der Release-Build von 1.5.0 schlug mit `AVLN2000: Button besitzt keine Eigenschaft TextWrapping`
fehl (`MainWindow.axaml`). `TextWrapping` ist keine Button-Eigenschaft in Avalonia — beide neuen
Buttons („Ungültigen Token entfernen und Anmeldung reparieren“, „Remote sicher bereinigen“)
verwenden jetzt korrekt einen `TextBlock` als Button-Inhalt. Keine funktionalen Änderungen
gegenüber 1.5.0, reiner Build-Fix.

### Version 1.5.0 — Selbstdiagnose & Selbstreparatur der GitHub-Authentifizierung

Referenzfall: Ein ungültiger `GITHUB_TOKEN`/`GH_TOKEN` in der Windows-Umgebung überschreibt eine
gültige, im GitHub-CLI-Keyring gespeicherte Anmeldung. Die App meldete das bisher fälschlich als
„Nicht bei GitHub eingeloggt“ und blockierte den Push, obwohl `gh auth status` mit bereinigter
Umgebung eine gültige Anmeldung zeigt.

- Strukturierte Authentifizierungsdiagnose mit eigenem Zustandsmodell (`AuthenticationState`):
  `Authenticated`, `AuthenticatedViaKeyring`, `AuthenticatedViaEnvironmentToken`,
  `InvalidEnvironmentToken`, `EnvironmentTokenOverridesValidKeyring`, `NotAuthenticated`,
  `MissingRequiredScopes`, `GitHubCliUnavailable`, `AuthenticationCheckFailed`.
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
  besteht, ob nur eine Berechtigung fehlt, oder ob eine echte Neuanmeldung nötig ist.
- Diagnosebericht-Export: redigierter Bericht (Version, OS, Git-/CLI-Version, Repository-Pfad,
  credential-freie Remote-URL, Branch, Auth-Status, Scopes) zum Weitergeben an Entwickler — niemals
  mit Tokens, Passwörtern oder rohen Umgebungsvariablen-Werten.
- Push wird nur noch blockiert, wenn eine zwingende Voraussetzung tatsächlich fehlt, und die
  Meldung nennt den genauen Grund plus ob eine automatische Reparatur verfügbar ist.

### Version 1.4.0 — Clean Export

Ergänzt den plattformübergreifenden Dialog „Projekt exportieren / Clean Export“ (siehe Abschnitt
oben). Die Minor-Version wurde erhöht, weil es sich um eine neue, rückwärtskompatible Funktion handelt.

### Version 1.3.0 — Sync-Preflight, Fehleranalyse, Auto-Update-Check

- **Sync-Preflight**: „Umgebung prüfen“ führt bei ausgewähltem Projekt alle neun Vor-Push-Checks in
  einem Schritt aus (siehe Abschnitt „Sync-Preflight“ oben).
- **Fehleranalyse**: Bekannte Pull-/Push-Fehlermuster werden erkannt und mit Klartext-Lösungshinweis
  angezeigt (siehe Abschnitt „Fehleranalyse“ oben).
- **Update-Check**: Die App prüft beim Start automatisch die GitHub Releases API auf neue Versionen
  und zeigt bei Verfügbarkeit ein Banner mit Direktdownload-Button. Läuft im Hintergrund, blockiert
  die App nicht und schlägt still fehl bei fehlendem Netz. Manuell auslösbar über „Auf Updates
  prüfen“.
- Der Windows-Installer erkennt eine vorhandene 1.x-Installation automatisch und aktualisiert sie
  in-place — kein manuelles Deinstallieren nötig.

## Lizenz und erlaubte Nutzung

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Die Software darf nach der [AI GitHub Manager Community-Use License](LICENSE) kostenlos heruntergeladen, installiert und für private, schulische sowie interne betriebliche Zwecke verwendet werden.

Ohne vorherige schriftliche Genehmigung ist insbesondere nicht erlaubt:

- Verkauf oder entgeltliche Weitergabe;
- Veröffentlichung eigener Installer oder Binärdistributionen;
- öffentliche Neuveröffentlichung veränderter Versionen außerhalb der normalen GitHub-Fork-Funktion;
- Angebot als bezahlter Dienst oder Bestandteil eines kommerziellen Produkts;
- Entfernung von Copyright-, Lizenz- oder Herkunftshinweisen; und
- Verwendung von Name oder Logo mit dem Eindruck einer offiziellen oder genehmigten Version.

Das Projekt ist **source available**, aber nicht unter einer OSI-anerkannten Open-Source-Lizenz veröffentlicht. Das Urheberrecht und alle nicht ausdrücklich eingeräumten Rechte verbleiben bei André Iljaschow / illeArts.

## Haftung und Eigenverantwortung

Die Software wird ohne Garantie und im gesetzlich zulässigen Umfang ohne Haftung für Datenverlust, beschädigte Repository-Historien, Fehlkonfigurationen, Zugriffsverluste, Ausfälle oder sonstige Schäden bereitgestellt.

Nutzer sind selbst verantwortlich für:

- aktuelle und überprüfbare Backups;
- Kontrolle von Remote, Branch, Diff und Repository-Status;
- Schutz von Tokens, Schlüsseln und vertraulichen Dateien;
- ausreichende GitHub- und Dateisystemberechtigungen;
- Rechtmäßigkeit und Lizenzierung der verarbeiteten Inhalte; und
- die Folgen ausgelöster Git-, Export- und Reparaturaktionen.

Gesetzlich zwingende Haftung bleibt unberührt. Maßgeblich ist der vollständige Text in [LICENSE](LICENSE).

## Datenschutz

Die Anwendung arbeitet lokal und betreibt nach aktuellem Stand keinen eigenen Telemetrie-, Analyse- oder Benutzerdienst. Netzwerkzugriffe erfolgen insbesondere zu GitHub, der GitHub API und konfigurierten Git-Remotes. Details stehen in [PRIVACY.md](PRIVACY.md).

## Sicherheitsmeldungen

Sicherheitslücken dürfen nicht zusammen mit Tokens, privaten Schlüsseln, Zugangsdaten oder vertraulichem Quellcode in öffentlichen Issues veröffentlicht werden. Der vorgesehene Meldeweg und die unterstützten Versionen stehen in [SECURITY.md](SECURITY.md).

## Drittanbieter

Avalonia, .NET, Git, GitHub CLI, Schriftarten und weitere Drittanbieter-Komponenten unterliegen eigenen Lizenzen und Bedingungen. Eine Übersicht befindet sich in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Unabhängigkeit und Marken

AI GitHub Manager ist ein unabhängiges Projekt und weder mit GitHub, Inc. noch mit Microsoft Corporation verbunden, von diesen gesponsert oder offiziell unterstützt.

GitHub, das GitHub-Logo, Microsoft, Windows, .NET, Apple, macOS, Linux und weitere Produktnamen oder Marken gehören ihren jeweiligen Rechteinhabern. Ihre Nennung dient ausschließlich der Beschreibung von Kompatibilität und Funktion.
