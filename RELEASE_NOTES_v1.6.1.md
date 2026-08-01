# AI GitHub Manager v1.6.1

AI GitHub Manager 1.6.1 erweitert die Anwendung um eine belastbare Selbstdiagnose und Selbstreparatur für typische GitHub-, Token-, Remote-, Build- und Installer-Probleme.

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

## Downloads

Das Release veröffentlicht reproduzierbar erzeugte Pakete für:

- Windows x64
- macOS x64
- macOS Apple Silicon (arm64)
- Linux x64

Die Pakete werden durch GitHub Actions direkt aus dem mit `v1.6.1` gekennzeichneten Quellstand erzeugt.

## Voraussetzungen

- Für GitHub-Funktionen: Git und GitHub CLI (`gh`).
- Für die Windows-Installer-Erstellung innerhalb der App: Inno Setup 6.
- Für Entwicklung aus dem Quellcode: .NET 8 SDK.

## Lizenz

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Download, Installation und eigene Nutzung sind gemäß der Datei `LICENSE` gestattet. Verkauf, fremde Binärdistributionen, kommerzielle Dienste und öffentliche Neuveröffentlichungen veränderter Versionen benötigen eine vorherige schriftliche Genehmigung.

## Hinweis

AI GitHub Manager ist ein unabhängiges Projekt und kein offizielles Produkt von GitHub, Inc. oder Microsoft Corporation.
