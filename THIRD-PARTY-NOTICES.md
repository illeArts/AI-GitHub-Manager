# Drittanbieterhinweise

Stand: 1. August 2026

AI GitHub Manager nutzt oder interagiert mit Komponenten und Diensten Dritter. Diese bleiben Eigentum ihrer jeweiligen Rechteinhaber und unterliegen ihren eigenen Lizenzen und Bedingungen.

## Direkt eingebundene NuGet-Pakete

Nach aktuellem Projektstand werden in der Desktop-Anwendung folgende Pakete verwendet:

| Komponente | Version | Zweck | Lizenzhinweis |
|---|---:|---|---|
| Avalonia | 11.2.3 | Cross-Platform-UI-Framework | MIT License |
| Avalonia.Desktop | 11.2.3 | Desktop-Laufzeit und Plattformintegration | MIT License |
| Avalonia.Themes.Fluent | 11.2.3 | Fluent-Oberflächentheme | MIT License |
| Avalonia.Fonts.Inter | 11.2.3 | Bereitstellung der Schriftfamilie Inter | SIL Open Font License 1.1 beziehungsweise Paketlizenz beachten |

Die vollständigen Lizenztexte und Abhängigkeiten sind den jeweiligen Paketen, deren Quellrepositorys und den beim Build erzeugten NuGet-Metadaten zu entnehmen. Bei Abweichungen gelten die Original-Lizenztexte der Drittanbieter.

## Laufzeit und externe Werkzeuge

AI GitHub Manager setzt je nach Plattform voraus oder kann folgende externe Werkzeuge verwenden:

| Komponente/Dienst | Verwendung |
|---|---|
| Microsoft .NET 8 | Laufzeit und SDK |
| Git | lokale Versionsverwaltung |
| GitHub CLI (`gh`) | GitHub-Anmeldung, Credential-Einrichtung und GitHub-Funktionen |
| GitHub und GitHub API | Hosting, Remote-Operationen und Update-Prüfung |
| Betriebssystem-Credential-Store | geschützte Speicherung der durch GitHub CLI verwalteten Zugangsdaten |

Diese Komponenten werden nicht durch die Projektlizenz von AI GitHub Manager lizenziert. Installation und Verwendung richten sich nach den jeweiligen Herstellerbedingungen.

## Marken

GitHub und das GitHub-Logo sind Marken von GitHub, Inc. Microsoft, Windows, .NET und zugehörige Zeichen sind Marken der Microsoft-Unternehmensgruppe. Apple, macOS und zugehörige Zeichen sind Marken von Apple Inc. Linux ist eine Marke von Linus Torvalds.

Die Nennung erfolgt ausschließlich zur Beschreibung von Kompatibilität und Funktion. AI GitHub Manager ist nicht mit diesen Rechteinhabern verbunden, von ihnen gesponsert oder offiziell unterstützt.

## Eigene Assets

Projektname, eigenes Logo, eigene Grafiken, Dokumentation und sonstige projektspezifische Assets unterliegen, soweit nicht ausdrücklich anders gekennzeichnet, der Datei `LICENSE` und bleiben urheberrechtlich geschützt.

## Pflegehinweis

Diese Datei muss aktualisiert werden, wenn neue Pakete, Schriften, Icons, Bibliotheken, SDKs oder eingebettete Assets aufgenommen oder deren Versionen geändert werden. Vor jedem öffentlichen Release sollte zusätzlich ein automatisierter Lizenz- und Abhängigkeitsbericht aus dem tatsächlich veröffentlichten Build erzeugt und archiviert werden.
