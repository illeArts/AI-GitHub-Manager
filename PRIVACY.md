# Datenschutzhinweise

Stand: 1. August 2026

## Grundsatz

AI GitHub Manager ist eine lokal ausgeführte Desktop-Anwendung. Das Projekt betreibt keinen eigenen Benutzerkonten-, Analyse-, Werbe- oder Telemetriedienst. Nach aktuellem Funktionsstand werden keine Nutzungsprofile an illeArts übertragen.

## Lokal verarbeitete Daten

Je nach Nutzung verarbeitet oder zeigt die Anwendung lokal insbesondere:

- GitHub-Benutzernamen und Anmeldestatus der GitHub CLI;
- Repository-Namen, Remote-Adressen und Branch-Informationen;
- lokale Projektpfade;
- Git-Status, Commit-Nachrichten und Diagnoseausgaben;
- Name und E-Mail-Adresse aus der lokalen Git-Konfiguration;
- Exportvorschauen, Dateinamen und Hinweise auf möglicherweise sensible Dateien; und
- Versionsinformationen für die Update-Prüfung.

Diese Daten bleiben grundsätzlich auf dem Gerät, soweit sie nicht im Rahmen einer vom Nutzer ausgelösten Git- oder GitHub-Aktion an den jeweiligen Remote-Dienst übertragen werden.

## Zugangsdaten

Die Anwendung speichert absichtlich keine GitHub-Tokens in eigenen JSON-, Log- oder Einstellungsdateien. Anmeldung und Credential-Verwaltung erfolgen über die offizielle GitHub CLI (`gh`) und den sicheren Credential Store des Betriebssystems.

Für Sicherheit, Berechtigungen, Aufbewahrung und Löschung dieser Zugangsdaten gelten zusätzlich die Einstellungen und Bedingungen des Betriebssystems, von Git, der GitHub CLI und GitHub.

## Netzwerkverbindungen

Netzwerkzugriffe können insbesondere stattfinden:

- zu GitHub und der GitHub API;
- zu den vom Nutzer konfigurierten Git-Remotes;
- zur GitHub Releases API für die Prüfung auf neue Versionen; und
- zu Paketquellen oder Installationsdiensten, wenn der Nutzer externe Installations- oder Updatevorgänge startet.

Für diese externen Dienste gelten deren eigene Datenschutzbestimmungen und Nutzungsbedingungen. AI GitHub Manager kontrolliert deren Verarbeitung nicht.

## Logs und Diagnoseinformationen

Diagnoseausgaben können lokale Pfade, Repository-Namen, Branches, Commit-Informationen oder Fehlermeldungen enthalten. Vor Veröffentlichung in Issues oder Support-Anfragen müssen Nutzer vertrauliche und personenbezogene Informationen entfernen.

Tokens, Passwörter, private Schlüssel, Wiederherstellungscodes und vertrauliche Repository-Inhalte dürfen niemals veröffentlicht werden.

## Exporte

Die Funktion „Clean Export“ prüft Dateien lokal anhand bekannter Namen, Pfade und Dateiendungen auf mögliche sensible Inhalte. Diese Erkennung ist eine Hilfestellung und keine Garantie. Nutzer müssen jeden Export vor Weitergabe selbst kontrollieren.

## Keine automatische Übermittlung an den Herausgeber

Nach aktuellem Stand sendet die Anwendung keine Absturzberichte, Nutzungsstatistiken, Projektdateien oder Zugangsdaten automatisch an André Iljaschow oder illeArts.

Sollten künftig optionale Telemetrie, Crash-Reporting, Cloud-Funktionen oder ein eigener Update-Dienst hinzukommen, müssen diese Hinweise vor Veröffentlichung entsprechend aktualisiert werden.

## Verantwortung des Nutzers

Nutzer sind verantwortlich für:

- die Rechtmäßigkeit der verarbeiteten Repository- und Personendaten;
- die Einhaltung interner Datenschutz- und Geheimhaltungsvorgaben;
- die sichere Konfiguration von GitHub, Git und Betriebssystem;
- die Prüfung von Logs, Screenshots und Exporten vor einer Weitergabe; und
- die Löschung nicht mehr benötigter lokaler Projekt- und Diagnosedaten.

## Unabhängiges Projekt

AI GitHub Manager ist ein unabhängiges Projekt und weder mit GitHub, Inc. noch mit Microsoft Corporation verbunden, von diesen gesponsert oder offiziell unterstützt.
