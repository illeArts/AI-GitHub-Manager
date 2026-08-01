# AI GitHub Manager

[![Version](https://img.shields.io/badge/version-1.4.0-blue)](#aktueller-funktionsstand)
[![Platforms](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#voraussetzungen)
[![License](https://img.shields.io/badge/license-proprietary%20community--use-orange)](LICENSE)

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

Aktuelle Projektversion: **1.4.0**

Noch sinnvoll zu verifizieren beziehungsweise auszubauen:

- Clean Export auf unterstützten macOS- und Linux-Zielsystemen manuell testen;
- Oberfläche und Lokalisierung des Exportdialogs vereinheitlichen;
- reproduzierbare Release-Artefakte für Windows, macOS und Linux automatisieren;
- Signierung, Prüfsummen und Release-Nachweise ergänzen;
- Regressionstests bei neuen Randfällen erweitern.

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
