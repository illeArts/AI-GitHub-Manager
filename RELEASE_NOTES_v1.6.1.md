# AI GitHub Manager v1.6.1

AI GitHub Manager 1.6.1 erweitert die Anwendung um eine belastbare Selbstdiagnose und Selbstreparatur für typische GitHub-, Token-, Remote-, Build- und Installer-Probleme.

## Empfohlener Download

### Windows 64-Bit

Für die normale Installation verwenden:

`AI_GitHub_Manager_Setup_1.6.1_win-x64.exe`

Das portable ZIP-Paket bleibt zusätzlich verfügbar und benötigt keine Installation.

Der Windows-Installer ist derzeit nicht mit einem kostenpflichtigen Code-Signing-Zertifikat signiert. Windows SmartScreen kann deshalb beim ersten Start einen Warnhinweis anzeigen. Die zugehörige `.sha256`-Datei ermöglicht die Prüfung, ob der Download unverändert ist.

### macOS und Linux

- macOS Intel: `AI-GitHub-Manager-v1.6.1-macos-x64.zip`
- macOS Apple Silicon: `AI-GitHub-Manager-v1.6.1-macos-arm64.zip`
- Linux x64: `AI-GitHub-Manager-v1.6.1-linux-x64.tar.gz`

## Neu in 1.6.1

- Vorsorgliche Erkennung, ob Inno Setup 6 unter Windows installiert ist.
- Der Button **„Inno Setup installieren“** erscheint nur, wenn Inno Setup 6 fehlt.
- Der Button öffnet ausschließlich die offizielle Download-Seite; die Anwendung installiert nichts ungefragt.
- Nach einer manuellen Installation wird die Umgebung erneut geprüft und der Hinweis automatisch ausgeblendet.

## Enthaltene Verbesserungen seit 1.4.0

- Strukturierte GitHub-Authentifizierungsdiagnose.
- Erkennung ungültiger `GH_TOKEN`- und `GITHUB_TOKEN`-Variablen, die eine gültige Keyring-Anmeldung überschreiben.
- Ein-Klick-Reparatur für den betroffenen Benutzer-Tokenzustand.
- Sichere Remote-URL-Normalisierung und Entfernung eingebetteter Zugangsdaten oder Platzhalter.
- Redigierter Diagnosebericht ohne Tokens und Passwörter.
- Vollständig zweisprachige Oberfläche in Deutsch und Englisch.
- **Build, Test & Push** für .NET-Projekte mit Abbruch vor dem Push, falls Build oder Tests fehlschlagen.
- Installer-Erstellung für Windows und macOS.
- Überarbeitetes Drei-Spalten-Layout der Hauptoberfläche.
- Build-Korrektur für Avalonia aus Version 1.5.1.

## Sicherheit

- Die Anwendung speichert keine GitHub-Tokens selbst.
- Anmeldung und Credential-Verwaltung bleiben bei GitHub CLI und dem sicheren Speicher des Betriebssystems.
- Kein automatischer Force-Push.
- Keine Löschung lokaler Dateien vor einem Pull.
- Konflikte und unsichere Zustände blockieren schreibende Aktionen.
- Clean Export verändert den Quellordner nicht.

## Downloads und Integrität

Das Release veröffentlicht reproduzierbar erzeugte Pakete für:

- Windows x64 als Installer und portables ZIP
- macOS x64
- macOS Apple Silicon (arm64)
- Linux x64

Alle veröffentlichten Programmdateien erhalten eine separate SHA-256-Prüfsumme. Die Pakete werden durch GitHub Actions direkt aus dem veröffentlichten Quellstand erzeugt.

## Voraussetzungen

- Für GitHub-Funktionen: Git und GitHub CLI (`gh`).
- Für die Windows-Installer-Erstellung innerhalb der App: Inno Setup 6.
- Für Entwicklung aus dem Quellcode: .NET 8 SDK.

## Lizenz

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Download, Installation und eigene Nutzung sind gemäß der Datei `LICENSE` gestattet. Verkauf, fremde Binärdistributionen, kommerzielle Dienste und öffentliche Neuveröffentlichungen veränderter Versionen benötigen eine vorherige schriftliche Genehmigung.

## Hinweis

AI GitHub Manager ist ein unabhängiges Projekt und kein offizielles Produkt von GitHub, Inc. oder Microsoft Corporation.
