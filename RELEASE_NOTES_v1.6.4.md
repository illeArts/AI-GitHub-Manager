# AI GitHub Manager v1.6.4

**Release v1.6.4** — behebt einen Datenmodell-Fehler bei der Zuordnung von
lokalen Projekten zu ihrem tatsächlichen GitHub-Repository und ergänzt die
dafür nötige Bedienoberfläche. Gebaut und getestet; der Windows-Installer
dieses Releases wurde in dieser Runde nicht auf einem echten Windows-System
manuell durchgeklickt (siehe „Test- und Build-Status").

## Neu in 1.6.4

### GitHub-Remote-Erkennung und Organisations-Repositories

- Der tatsächliche GitHub-Link eines Projekts (`RepositoryWebUrl`,
  `RepositoryOwner`, `RepositoryName`) wird jetzt ausschließlich aus dem
  echten `git remote get-url origin` (via `RemoteDetectionService`, auf
  Basis des bestehenden `RemoteUrlNormalizer`) oder einer validierten
  manuellen Eingabe abgeleitet — nie aus dem aktuell angemeldeten
  GitHub-Account kombiniert mit dem lokalen Ordner-/Projektnamen.
  Organisations-Repositories (z. B. `github.com/Organisation/Projekt`)
  werden dabei genauso behandelt wie persönliche Repositories.
- Unterstützte Remote-Formate: `https://github.com/owner/repo(.git)`,
  `git@github.com:owner/repo.git`, `ssh://git@github.com/owner/repo.git`.
- Fehlt ein `origin`-Remote oder lässt er sich nicht als GitHub-Adresse
  erkennen, wird niemals eine Adresse erfunden — stattdessen öffnet die App
  den manuellen Eingabedialog mit einer klaren deutschen Fehlermeldung.

### Projekt-Kontextmenü

- Rechtsklick auf einen Eintrag in der Projektliste öffnet jetzt ein
  Kontextmenü: „Auf GitHub öffnen“, „GitHub-Link kopieren“, „GitHub-Link
  festlegen …“/„GitHub-Link bearbeiten …“ (je nachdem, ob bereits ein Link
  bekannt ist), „Remote erneut erkennen“, „Projekt speichern“, „Aus Manager
  entfernen …“.
- Vollständiger Projekt- und Owner-/Organisationsname sind als Tooltip
  hinterlegt, wenn der angezeigte Text abgeschnitten wird.
  `AutomationProperties.Name` ist auf jedem Menüeintrag gesetzt.
- Funktioniert plattformneutral (reines Avalonia-XAML, keine
  Windows-spezifische Umsetzung); Windows, macOS und Linux bleiben
  kompilierbar.

### Manuelles Festlegen/Bearbeiten von GitHub-Links

- Eigener Dialog für „GitHub-Link festlegen“ und „GitHub-Link bearbeiten“:
  zeigt bei „Bearbeiten“ den vorhandenen Link vorausgefüllt an, akzeptiert
  vollständige GitHub-Weblinks sowie die Kurzform `owner/repo`, validiert
  die Eingabe vor dem Speichern, zeigt bei ungültiger Eingabe eine
  verständliche deutsche Fehlermeldung, und lässt sich ohne Datenänderung
  abbrechen.

### Remote erneut erkennen

- Liest `origin` erneut aus und vergleicht mit dem gespeicherten Link.
- Bei unverändertem Remote wird ohne Rückfrage gespeichert.
- Weicht der neu erkannte Remote vom gespeicherten Link ab (z. B. nach
  einer Repository-Übertragung auf eine Organisation), fragt die App vor
  dem Überschreiben explizit nach Bestätigung (alter und neuer Link werden
  angezeigt) — eine bestehende, insbesondere manuell gesetzte Zuordnung
  wird nie ungefragt überschrieben.

### Sicheres „Aus Manager entfernen“

- Entfernt ausschließlich den Eintrag aus der Projektverwaltung
  (`projects.json`). Der lokale Projektordner und das GitHub-Repository
  selbst werden dabei nie gelöscht — das ist bewusst nicht Teil dieses
  Menüpunkts. Vor dem Entfernen erscheint eine verständliche Bestätigung,
  die das ausdrücklich benennt.

### Datenmodell und Kompatibilität

- `ManagedProject` um `RepositoryWebUrl`, `RepositoryOwner`,
  `RepositoryName` und `RemoteSource` (`Unknown` / `GitOrigin` / `Manual` /
  `Imported`) erweitert. Bestehende Felder (`Owner`, `RemoteUrl`) bleiben
  aus Kompatibilitätsgründen erhalten und werden von allen Schreibpfaden
  synchron mitgeführt.
- Alte, vor 1.6.4 gespeicherte `projects.json`-Dateien ohne diese neuen
  Felder laden unverändert weiter; die neuen Felder erhalten sichere
  Standardwerte, ohne vorhandene Daten zu überschreiben oder eine Adresse
  zu erfinden.

### Test-Isolation für projects.json

- Ein Testfehler wurde auf eine geteilte Zustandsquelle zurückgeführt und
  behoben: `MainWindowViewModel` lädt beim Start unabhängig vom
  Testkontext im Hintergrund Projekte aus dem produktiven
  `JsonProjectStore`-Standardpfad. Tests, die den Konstruktor ohne
  eigenen Store aufriefen, teilten sich dadurch versehentlich dieselbe
  reale Datei — ein in einem Test hinterlassenes Projekt konnte über
  diesen Hintergrundvorgang in einen völlig anderen, bereits laufenden
  Test hineinlaufen und dort z. B. den lokalen Pfad überschreiben.
  `MainWindowViewModel` hat dafür jetzt einen zweiten, rein internen
  Test-/DI-Konstruktor, der einen isolierten `JsonProjectStore` entgegen-
  nimmt; die produktive App verhält sich dadurch unverändert.

## Sicherheit

- Kein automatisches Erfinden einer GitHub-Adresse unter keinen Umständen.
- „Aus Manager entfernen“ löscht nachweislich (durch einen echten
  Dateisystem-Regressionstest abgesichert) weder den lokalen Ordner noch
  Dateien darin.
- Bestehende Sicherheitsmechanismen aus 1.6.3 (kein automatischer
  `git reset --hard`, kein Force-Push ohne `--force-with-lease`, sicherer
  Pull, Git-Lock-Schutz) bleiben unverändert erhalten.

## Voraussetzungen

- Für GitHub-Funktionen: Git und GitHub CLI (`gh`).
- Für die Windows-Installer-Erstellung innerhalb der App: Inno Setup 6.
- Für Entwicklung aus dem Quellcode: .NET 8 SDK.

## Test- und Build-Status

- Tests: 275/275 grün (`dotnet test`, Release-Konfiguration), 0 Warnings,
  0 Errors beim Build. Volle, ungefilterte Testsuite dreimal hintereinander
  ausgeführt, jedes Mal 275/275 bestanden, 0 übersprungen.
- Neue/erweiterte Regressionstests decken ab: HTTPS-/SSH-Remotes für
  persönliche und Organisations-Repositories (inkl. `ssh://`-Form), Remotes
  mit und ohne `.git`, fehlendes `origin`, syntaktisch vorhandener aber
  nicht als GitHub-Adresse erkennbarer `origin`, manuelle Linkeingabe
  (gültig/ungültig), Bearbeiten eines vorhandenen Links, unveränderte und
  geänderte Remote-Neuerkennung (inkl. Bestätigungspflicht), alte
  `projects.json` ohne neue Felder, Speichern/Laden aller neuen Felder,
  und dass „Aus Manager entfernen“ den lokalen Ordner nachweislich nicht
  löscht.
- Verbindlicher Regressionstest gegen ein echtes temporäres Git-Repository:
  lokaler Projektname `bullbear`, `origin =
  https://github.com/illeArts-Finance/bullbear.git` → erkannter Web-Link
  exakt `https://github.com/illeArts-Finance/bullbear` (und ausdrücklich
  *nicht* `https://github.com/illeArts/bullbear`).
- Windows-Installer und macOS-App-Bundle dieses Releases wurden in dieser
  Runde **nicht** auf echten Windows-/macOS-Systemen manuell durchgeklickt
  — das steht noch aus (siehe „Bekannte Einschränkungen“).

## Bekannte Einschränkungen

- Developer-ID-Signierung und Notarisierung für macOS sind weiterhin nicht
  umgesetzt (unverändert seit 1.6.3) — die Gatekeeper-Warnung „unbekannter
  Entwickler" bleibt bestehen.
- Manueller End-to-End-Test des Windows-Installers (Installation, Start,
  Laden bestehender Projekte, Kontextmenü, „Auf GitHub öffnen“, „Aus
  Manager entfernen“) auf einem echten Windows-System steht noch aus.
- Eigener Hilfe-Abschnitt zu den „Erweiterten Befehlen“ (Rebase/Cherry-
  Pick/Force Push/Reset/Hard Reset/Clean) fehlt weiterhin — unverändert
  seit 1.6.3.
- Vollständige Tastatur-/Screenreader-Durchgängigkeit über die gesamte
  Anwendung ist weiterhin nicht flächendeckend geprüft — unverändert seit
  1.6.3, in 1.6.4 um das neue Kontextmenü ergänzt.

## Lizenz

Copyright © 2026 André Iljaschow / illeArts. Alle Rechte vorbehalten.

Download, Installation und eigene Nutzung sind gemäß der Datei `LICENSE`
gestattet. Verkauf, fremde Binärdistributionen, kommerzielle Dienste und
öffentliche Neuveröffentlichungen veränderter Versionen benötigen eine
vorherige schriftliche Genehmigung.

## Hinweis

AI GitHub Manager ist ein unabhängiges Projekt und kein offizielles Produkt
von GitHub, Inc. oder Microsoft Corporation.
