# AI GitHub Manager v1.6.3

**Entwurf — Meilensteine 1–4 vollständig implementiert, gebaut und getestet.
Meilenstein 5 (Doku/finale Builds) läuft. Noch NICHT release-fertig: die
Developer-ID-Signierung/Notarisierung für macOS steht noch aus (siehe
„Bekannte Einschränkungen").

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

- Das bisherige, vage „Update"-Feld ist durch ein strukturiertes Dropdown mit
  12 verständlich benannten Vorgängen ersetzt (Änderungen prüfen,
  Aktualisieren, Änderungen hochladen, Commit erstellen, Commit erstellen
  und hochladen, Repository-Status anzeigen, Branch erstellen, Branch
  wechseln, Änderungen zwischenspeichern, Zwischengespeicherte Änderungen
  wiederherstellen, Merge durchführen, Änderungen vergleichen). Vorauswahl
  ist immer „Aktualisieren".
  „Änderungen prüfen" und „Änderungen herunterladen" wurden bewusst zu einem
  Eintrag zusammengeführt (beides ist technisch `git fetch`), um keine
  irreführenden Duplikate anzuzeigen.
- Direkt unter dem Dropdown zeigt ein Erklärungsfeld für jeden Vorgang:
  „Geeignet für", „Was wird verändert?", „Was bleibt unverändert?", das
  Risiko in Textform sowie die Risikostufe (Sicher / Vorsicht / Erweitert /
  Gefährlich — nie nur farblich codiert) und den technischen Befehl.
- Für die aktuell ausführbaren Vorgänge (Status, Aktualisieren, Commit
  erstellen und hochladen) erscheint zusätzlich eine „Was passiert
  jetzt?"-Schrittliste mit Zusammenfassung.
- Fehlermeldungen sind jetzt vollständig zweisprachig (Deutsch/Englisch) und
  decken zusätzliche Fälle ab: divergierte Branches beim Aktualisieren
  („fatal: Not possible to fast-forward, aborting."), fehlender
  Upstream-Branch, Detached HEAD, unterbrochener Merge/Rebase,
  Datei-/Berechtigungsfehler — jeweils mit Ursache, Lösungsvorschlag und
  vollständig erhaltenem technischem Originaltext.
- „Erweiterte Befehle" (Rebase, Cherry-Pick, Force Push, Reset, Hard Reset,
  Clean) sind in einem eigenen, standardmäßig eingeklappten Bereich
  untergebracht, werden nie automatisch ausgewählt oder gespeichert und
  lassen sich nur nach einem expliziten Bestätigungsdialog ausführen, der
  Repository, Branch, betroffenen Umfang und Wiederherstellbarkeit nennt.
  Bei Hard Reset, Clean und Force Push muss zusätzlich der aktive
  Branch-Name exakt eingetippt werden. Kein Dialog-Button ist per Enter
  auslösbar. Force Push verwendet ausschließlich `--force-with-lease`,
  niemals ein ungeschütztes `--force`.

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

## Test- und Build-Status (Stand Meilenstein 4, Sandbox-Build)

- Tests: 249/249 grün (`dotnet test`, Release-Konfiguration), 0 Warnings,
  0 Errors beim Build. Deckt u. a. ab: Vorgangsmodell (Risikostufen,
  keine doppelten Einträge, Vorauswahl), Persistenz (gefährliche/erweiterte
  Auswahl wird nie als Standard gespeichert), zweisprachige
  Fehlerübersetzung (11 bestehende + 5 neue GitErrorKind-Fälle),
  Vorabprüfungs-Vorschau, echte (nicht gemockte) Integrationstests für
  Clean/Reset/Hard-Reset/Rebase/Cherry-Pick/Force-Push gegen echte
  Git-Repositories, Bestätigungs-Gating auf ViewModel-Ebene.
- Cross-Builds aus der Entwicklungsumgebung (Linux/ARM64) für alle fünf
  Zielplattformen erfolgreich: `win-x64` (PE32+), `osx-arm64` (Mach-O
  arm64), `osx-x64` (Mach-O x86_64), `linux-x64` (ELF x86-64),
  `linux-arm64` (ELF aarch64) — jeweils korrekte Architektur verifiziert,
  keine plattformfremden Binärdateien in den Ausgabeordnern.
- Echter Laufzeittest: **nur `linux-arm64`**, da dies die native Architektur
  der Build-Umgebung ist — der self-contained Build wurde unter Xvfb
  (virtueller Framebuffer) tatsächlich gestartet und lief fehlerfrei bis
  zum kontrollierten Abbruch (kein Absturz, keine Fehlerausgabe).
- **`macOS-arm64` wurde in dieser Umgebung NICHT real getestet** — hier
  existieren weder `codesign`, `plutil`, `xattr`, `ditto` noch `spctl`. Der
  reale macOS-Test (Bundle-Assembly, Ad-hoc-Signierung, `codesign --verify`,
  tatsächlicher App-Start) muss wie in 1.6.2 über
  `scripts/verify-macos-app-bundle.sh` auf einem echten Mac erfolgen
  (Skript ist bereits auf `VERSION="1.6.3"` aktualisiert).
- `win-x64` und `osx-x64` wurden nur cross-kompiliert, nicht ausgeführt
  (kein Windows- bzw. Intel-Mac-System in dieser Umgebung verfügbar).
- Update-Erkennung erneut geprüft: `UpdateCheckService`-Fallback-Version
  steht auf „1.6.3".

## Bekannte Einschränkungen (Stand Meilenstein 4)

- Developer-ID-Signierung und Notarisierung für macOS sind noch nicht
  umgesetzt (siehe Backlog-Punkt „1.6.3: Developer-ID-Signierung +
  Notarisierung verbindlich einplanen") — ohne echtes Zertifikat kann das
  in dieser Umgebung nicht nachgeholt werden. Bis dahin bleibt die
  Gatekeeper-Warnung „unbekannter Entwickler" für macOS-Nutzer bestehen
  (Workaround: Rechtsklick → Öffnen, oder `xattr -dr com.apple.quarantine`).
- Das Hilfe-Fenster enthält noch keinen eigenen Abschnitt zu den
  „Erweiterten Befehlen" (Rebase/Cherry-Pick/Force Push/Reset/Hard
  Reset/Clean) — die Bedienoberfläche selbst erklärt jedes dieser Kommandos
  bereits vollständig direkt im Bestätigungsdialog.
- Volle Tastatur-/Screenreader-Durchgängigkeit wurde nur an den zentralen
  Bedienelementen (Dropdown, Ausführen-Buttons) mit `AutomationProperties`
  ergänzt, nicht flächendeckend für die gesamte Anwendung geprüft.

## Lizenz

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Download, Installation und eigene Nutzung sind gemäß der Datei `LICENSE`
gestattet. Verkauf, fremde Binärdistributionen, kommerzielle Dienste und
öffentliche Neuveröffentlichungen veränderter Versionen benötigen eine
vorherige schriftliche Genehmigung.

## Hinweis

AI GitHub Manager ist ein unabhängiges Projekt und kein offizielles Produkt
von GitHub, Inc. oder Microsoft Corporation.
