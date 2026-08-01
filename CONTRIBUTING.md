# Mitwirken an AI GitHub Manager

Vielen Dank für das Interesse an AI GitHub Manager. Beiträge sollen die Anwendung sicherer, verständlicher und plattformübergreifend zuverlässiger machen.

## Vor dem Beitrag

Bitte prüfe zunächst:

- ob bereits ein passendes Issue oder ein Pull Request existiert;
- ob das Verhalten mit der aktuellen Version reproduzierbar ist;
- ob Logs, Screenshots und Beispieldateien frei von Tokens, Zugangsdaten, privaten Repository-Inhalten und personenbezogenen Daten sind; und
- ob der geplante Beitrag zum kompakten, kontrollierten Charakter der Anwendung passt.

## Fehler melden

Ein guter Fehlerbericht enthält:

- Betriebssystem und Version;
- Version von AI GitHub Manager;
- Versionen von Git, GitHub CLI und .NET;
- nachvollziehbare Schritte;
- erwartetes und tatsächliches Verhalten;
- relevante, bereinigte Fehlermeldungen; und
- Hinweise, ob Repository, Remote oder Branch besondere Eigenschaften besitzen.

Sicherheitslücken gehören nicht in öffentliche Issues. Verwende dafür den in [SECURITY.md](SECURITY.md) beschriebenen vertraulichen Meldeweg.

## Änderungen einreichen

1. Repository über GitHub forken.
2. Einen klar benannten Branch erstellen.
3. Änderungen klein und fachlich zusammenhängend halten.
4. Bestehenden Stil, Nullable-Konfiguration und Projektstruktur beachten.
5. Tests ergänzen oder aktualisieren.
6. Vor dem Pull Request Build und Tests ausführen.
7. Pull Request mit Zweck, Umsetzung, Risiken und Prüfnachweis beschreiben.

Beispiel:

```bash
dotnet restore AI.GitHubManager.sln
dotnet build AI.GitHubManager.sln -c Release
dotnet test AI.GitHubManager.sln -c Release --no-build
```

## Sicherheitsanforderungen

Beiträge dürfen insbesondere nicht:

- Tokens, Passwörter, Schlüssel oder Zertifikate speichern oder protokollieren;
- ungeprüft Shell-Befehle aus Benutzereingaben zusammensetzen;
- `git push --force` automatisch ausführen;
- Dateien oder Branches ohne klare Bestätigung löschen;
- Konflikte automatisch und stillschweigend überschreiben;
- Sicherheitswarnungen oder Preflight-Prüfungen umgehen; oder
- Telemetrie, Tracking oder externe Datenübertragung ohne ausdrückliche Dokumentation und Einwilligung ergänzen.

## Abhängigkeiten und Assets

Neue NuGet-Pakete, Schriften, Icons, Bilder oder sonstige Drittanbieter-Inhalte müssen:

- technisch erforderlich sein;
- aus einer nachvollziehbaren Quelle stammen;
- eine mit dem Projekt vereinbare Lizenz besitzen;
- in `THIRD-PARTY-NOTICES.md` dokumentiert werden; und
- dürfen keine ungeklärten Marken- oder Urheberrechte enthalten.

## Lizenzierung von Beiträgen

Mit dem Einreichen eines Beitrags bestätigst du, dass du zur Bereitstellung berechtigt bist und keine Rechte Dritter verletzt.

Du behältst das Urheberrecht an deinem eigenen Beitrag. Gleichzeitig räumst du André Iljaschow / illeArts ein dauerhaftes, weltweites, nicht ausschließliches, unentgeltliches und unterlizenzierbares Recht ein, den Beitrag im Projekt zu nutzen, zu verändern, zu veröffentlichen, zu vertreiben und unter der aktuellen oder einer zukünftigen Projektlizenz bereitzustellen.

Reiche keinen Beitrag ein, wenn du diese Rechte nicht einräumen darfst.

## Annahme von Beiträgen

Es besteht kein Anspruch auf Annahme, Zusammenführung, Veröffentlichung oder Support eines Beitrags. Änderungen können aus technischen, sicherheitsbezogenen, lizenzrechtlichen oder produktstrategischen Gründen abgelehnt oder angepasst werden.

## Umgang

Sachliche Kritik und klare technische Diskussionen sind willkommen. Persönliche Angriffe, Diskriminierung, Belästigung, Drohungen oder die Veröffentlichung privater Informationen werden nicht akzeptiert.
