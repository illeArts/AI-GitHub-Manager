# Sicherheitsrichtlinie

## Unterstützte Versionen

Sicherheitskorrekturen werden grundsätzlich für die aktuelle veröffentlichte Haupt- beziehungsweise Minor-Version geprüft. Ältere Builds können veraltet sein und sollten nicht als sicher vorausgesetzt werden.

| Version | Status |
|---|---|
| 1.4.x | Unterstützt |
| < 1.4 | Nicht mehr unterstützt |

## Sicherheitslücken melden

Bitte veröffentliche vermutete Sicherheitslücken, Zugangsdaten, Tokens, private Repository-Inhalte oder personenbezogene Daten **nicht** in einem öffentlichen Issue.

Nutze bevorzugt GitHub Private Vulnerability Reporting im Bereich **Security → Report a vulnerability**, sofern diese Funktion für das Repository verfügbar ist. Falls keine vertrauliche Meldefunktion angeboten wird, eröffne lediglich ein neutrales Issue ohne technische Details und bitte um einen privaten Kontaktweg.

Eine gute Meldung enthält:

- betroffene Version und Betriebssystem;
- nachvollziehbare Schritte zur Reproduktion;
- mögliche Auswirkungen;
- betroffene Dateien oder Funktionen;
- einen sicheren Proof of Concept ohne fremde Daten; und
- bekannte Gegenmaßnahmen.

## Umgang mit Meldungen

Meldungen werden nach Möglichkeit bestätigt, eingeordnet und behoben. Es besteht keine garantierte Reaktions- oder Behebungsfrist. Bitte veröffentliche technische Details erst nach Abstimmung oder nachdem eine angemessene Behebungsmöglichkeit bestand.

## Sicherheitsmodell

AI GitHub Manager:

- speichert absichtlich keine GitHub-Tokens in eigenen Einstellungsdateien;
- verwendet für die Anmeldung die offizielle GitHub CLI (`gh`) und den Credential Store des Betriebssystems;
- führt keinen automatischen `git push --force` aus;
- bricht bei erkannten Konflikten ab, statt Änderungen blind zu überschreiben; und
- erwartet, dass Nutzer Remote, Branch, Diff, Berechtigungen und Backups selbst prüfen.

Diese Schutzmaßnahmen ersetzen keine sichere Systemkonfiguration. Nutzer bleiben für ihr GitHub-Konto, lokale Zugriffsrechte, Repository-Berechtigungen, Secrets, Backups und die Prüfung ausgeführter Aktionen verantwortlich.

## Nicht als Sicherheitslücke behandeln

Folgende Punkte sind ohne zusätzliche konkrete Schwachstelle keine Sicherheitslücke des Projekts:

- verlorene oder falsch konfigurierte GitHub-Zugangsdaten;
- unzureichende Repository-Berechtigungen;
- Git-Konflikte oder Non-Fast-Forward-Fehler;
- Schäden durch manuelle Änderungen außerhalb der Anwendung;
- Nutzung auf nicht mehr unterstützten Betriebssystemen oder Versionen; und
- Social Engineering, Phishing oder kompromittierte Drittanbieter-Systeme.

## Keine Geheimnisse in Meldungen

Vor dem Anhängen von Logs, Screenshots oder Exporten müssen mindestens folgende Inhalte entfernt werden:

- Tokens, Passwörter und Wiederherstellungscodes;
- private Schlüssel und Zertifikate;
- private Repository-URLs;
- lokale Benutzernamen und Pfade;
- E-Mail-Adressen und personenbezogene Daten; und
- vertraulicher Quellcode oder Geschäftsdaten.
