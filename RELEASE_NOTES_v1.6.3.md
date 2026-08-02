# AI GitHub Manager v1.6.3

**Entwurf — wird im Rahmen der v1.6.3-Entwicklung laufend erweitert. Nicht release-fertig.**

AI GitHub Manager 1.6.3 bringt macOS und Linux auf Funktionsparität mit Windows
(Einstellungen, Benutzerhandbuch/Hilfe, Über-Dialog) und macht die Git-Bedienung
für Einsteiger verständlicher: benannte Vorgänge mit Risikostufen, Vorabprüfungen,
lesbare Erfolgs-/Fehlermeldungen — bei vollem Zugriff auf die technischen
Originalausgaben.

## Empfohlener Download

_Wird nach Abschluss der Builds ergänzt._

## Neu in 1.6.3

### Plattform-Parität (macOS, Linux, Windows)

- Einstellungen, Benutzerhandbuch/Hilfe und Über-Dialog sind jetzt auf allen
  drei Plattformen erreichbar (zuvor teilweise nur unter Windows sichtbar,
  je nach installierter Build-Version).
- macOS erhält zusätzlich eine native Anwendungsmenüleiste
  (⌘, für Einstellungen, ⌘? für Hilfe, ⌘Q zum Beenden, „Über AI GitHub
  Manager" im Anwendungsmenü).
- Über-Dialog zeigt jetzt zusätzlich: Build-Version, Commit-Hash (sofern beim
  Build verfügbar), Betriebssystem, CPU-Architektur, .NET-Version, Lizenz,
  Copyright, GitHub-Projektlink (sicher im Systembrowser geöffnet),
  Datenschutzhinweis und den Hinweis „Kein Produkt von GitHub oder Microsoft".

### Verständliche Git-Bedienung

_Wird im weiteren Verlauf von 1.6.3 ergänzt (Vorgangsmodell, Risikostufen,
Vorabprüfungen, lesbare Erfolgs-/Fehlermeldungen)._

## Sicherheit

- Kein automatischer `git reset --hard`.
- Keine automatische Löschung lokaler Dateien.
- Kein Force-Push ohne ausdrückliche Freigabe (`--force-with-lease` bevorzugt).
- Bestehende Sicherheitsmechanismen aus 1.6.2 (sicherer Pull, Git-Lock-Schutz,
  plattformsichere Update-Auswahl) bleiben unverändert erhalten.

## Voraussetzungen

- Für GitHub-Funktionen: Git und GitHub CLI (`gh`).
- Für die Windows-Installer-Erstellung innerhalb der App: Inno Setup 6.
- Für Entwicklung aus dem Quellcode: .NET 8 SDK.

## Lizenz

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Download, Installation und eigene Nutzung sind gemäß der Datei `LICENSE`
gestattet. Verkauf, fremde Binärdistributionen, kommerzielle Dienste und
öffentliche Neuveröffentlichungen veränderter Versionen benötigen eine
vorherige schriftliche Genehmigung.

## Hinweis

AI GitHub Manager ist ein unabhängiges Projekt und kein offizielles Produkt
von GitHub, Inc. oder Microsoft Corporation.
