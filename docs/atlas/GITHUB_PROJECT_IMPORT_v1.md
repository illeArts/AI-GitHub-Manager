# Atlas-Baustein: Sicherer GitHub-Projektimport

- Stabile ID: `ai-github-manager:component:github-project-import`
- Spezifikationsversion: **1.0.0**, 2026-10-28
- Schema-Bezug: Atlas DATA-CONTRACT v0.1; Spezifikationsreife `review`, Integration `component_tested`. Kein automatisches Release- oder Produktfreigabe-Gate.
- Auftrag: [AI GitHub Manager #13](https://github.com/illeArts/AI-GitHub-Manager/issues/13), [Atlas Blueprint #1](https://github.com/illeArts/Atlas-Blueprint/issues/1).
- Referenzimplementierung: `illeArts/AI-GitHub-Manager`, Branch `codex/13-safe-project-import`, auf `main` ab `18f0b44`. Quellpfad: `src/AI.GitHubManager.Core/Projects/ProjectImportService.cs`.
- Datenklasse: veröffentlichbare Vertragsspezifikation. Echte absolute Hostpfade, private Repository-Inhalte, Tokens und Diagnoseausgaben sind keine Atlas-Publikationsdaten.

## Zweck / Purpose

Ein ausdrücklich ausgewähltes, auch privates GitHub-Repository vollständig im Sinne eines normalen Git-Clone samt Historie in genau den bestätigten lokalen Ordner importieren und dauerhaft verknüpfen. **Repository-Liste aktualisieren / Refresh repository list** synchronisiert ausschließlich Metadaten; **Projekt importieren / Import project** ist eine eigene Aktion.

Die Spezifikation ist projektneutral wiederverwendbar. Sie schafft weder eine gemeinsame Codebibliothek noch eine Integration des GitHub Managers in einen Atlas-Viewer. Produktcode und dessen Lizenz bleiben im Produktrepository.

## Zustände und Übergänge

`not-linked → preflight → empty-target | matching-repo | occupied-or-foreign-target`

- `empty-target → cloning → publishing → saving → linked`
- `matching-repo → offer-link-or-update → linking | checking-fast-forward → saving → linked`
- `occupied-or-foreign-target → blocked → choose-other-target`
- Jede ausführende Phase kann nach `failed` oder `cancelled` wechseln. Keine neue aktive Zuordnung bei Fehler/Abbruch.
- Eine vorherige gültige Zuordnung bleibt bei Fehler aktiv. Ein erfolgreich autorisiertes Update wird bei einem anschließenden Speicherfehler nicht automatisch zurückgesetzt.

## Eingaben und Schnittstellen

Eingaben: ausgewählter GitHub-Remote (Host/Owner/Name), API-geprüfte Repository-ID, absoluter Zielpfad, explizite Entscheidung bei vorhandenem Repository, CancellationToken und Phasenfortschritt. Keine Token-Eingabe.

Referenzschnittstellen: `IProjectImportCommands` kapselt Git/gh; `ProjectImportService.InspectAsync` liefert `ImportInspection` mit Branch, Upstream, Dirty, Ahead/Behind und Operationssperre; `ImportAsync` liefert erst nach erfolgreichem Dateivorgang ein `ImportResult`. Der konsumierende Adapter bestätigt `linked` erst nach erfolgreichem atomarem Speichern.

Persistenz ausschließlich lokal: bestehende Projekt-ID, GitHub-Repository-ID, Owner/Name, Web-URL, kanonischer Remote, plattformspezifischer absoluter Pfad, UTC-Änderungszeit. Portable Atlas-Profile enthalten höchstens Repo-/Komponentenreferenzen; absolute Hostpfade verbleiben in der privaten Standortzuordnung.

## UX-Vertrag

Aktionen → Projekt: Repository-Liste aktualisieren; Projekt importieren; Projekt speichern; Diagnosebericht exportieren. Bestehende Arbeitsabläufe erhalten.

Ziel explizit eingeben oder auswählen; Repository und vollständigen Zielpfad vor Beginn bestätigen. Kein versteckter Unterordner. Bei passendem Repo **Nur verknüpfen / Link only**, **Sicher aktualisieren / Update safely** (nur wenn Vorprüfung dies zulässt), **Abbrechen / Cancel**. Dirty, ignorierte Dateien, fehlender Upstream, Ahead/divergent, detached, Git-Sperre und unterbrochener Vorgang verhindern Update. Ahead/Behind vor Fetch sind als Momentaufnahme gekennzeichnet; nach Fetch erneut prüfen.

Belegte/fremde/unsichere Ziele: konkrete lokalisierte Warnung und **Anderen/leeren Ordner wählen / Choose another/empty folder**. Fortschritt und Abbruch ohne blockierten UI-Thread. Tastaturzugang, benannte Pfadeingabe, Escape zum Abbruch; keine gefährliche voreingestellte Enter-Aktion.

DE/EN-Ressourcen für sämtliche neuen Texte, Phasen, Dialoge und Fehler. Referenz: eingebettete `Import.de.json` und `Import.en.json`, stabile Ressourcenschlüssel, englischer Fallback. Weitere Sprachkataloge sind ergänzbar; Produktauswahl separat registrieren.

## Sicherheitsinvarianten

1. Vorhandene lokale Dateien niemals ungefragt überschreiben oder löschen. Kein automatischer Hard-Reset, Force-Pull, Rebase oder Stash als Teil des Imports.
2. Git/gh, bestehende CLI-Anmeldung und Repository-Zugriff prüfen. Keine zusätzlichen PATs speichern oder Secret-Ausgaben protokollieren.
3. Prozessbezogener `gh auth git-credential`-Helper für GitHub-Netzwerkaufrufe. Weder globale noch lokale Git-Konfiguration überschreiben. SSH-GitHub-Remotes für importierte Projekte nur im Kindprozess auf HTTPS umleiten; gespeicherte origin-Adresse erhalten.
4. Absolute Pfade normalisieren, Vorfahren auf Symlinks/Junctions/Reparse Points und fremde Repositories prüfen. Direkt vor Schreib-/Veröffentlichungsschritten erneut prüfen.
5. Neues Ziel: in einen eindeutigen Nachbarordner auf demselben Volume klonen, prüfen, dann ohne Ersetzen veröffentlichen. Bestehender leerer Zielordner darf ausschließlich nichtrekursiv entfernt werden; zwischenzeitlich hinzugekommene Dateien blockieren diesen Schritt.
6. Bestehendes Repo: kanonische Remote-Identität und Repository-Wurzel prüfen. Externe Git-Verzeichnisse/Worktrees und Alternates in v1 blockieren. Git-Metadaten mit Reparse Points ablehnen.
7. Nur Link nach erneuter Prüfung: keine Projektdateien/Config ändern, kein Fetch. Update ausschließlich nach ausdrücklicher Zustimmung und erneuter Prüfung vor/nach Fetch; Fast-Forward nachweisen; ignorierte Dateien nicht überschreiben. Hooks/Autostash ausgeschaltet.
8. Zielbezogene App-Sperre gemeinsam mit vorhandenen Git-Diensten verwenden. Git-Locks/unterbrochene Vorgänge beim Update blockieren und niemals als Importreparatur entfernen.
9. Abbruch beendet aktive Git-/Kindprozesse. Keine destruktive Fehlerbereinigung; temporäre Clone-Ordner bleiben ggf. zur Prüfung erhalten.
10. Atomare lokale Zuordnung und In-Memory-Rollback bei Speicherfehler. Status/Pull/Commit/Push anschließend auf dem importierten Pfad ausführen; erster Commit eines leeren Remotes benötigt Upstream-Einrichtung.

## Pflicht-Testmatrix und Nachweise

| Fall | Referenznachweis |
| --- | --- |
| Leeres/fehlendes Ziel, exakter Pfad | echte isolierte Git-Integrationstests |
| Passendes sauberes/dirty Repo, Link/Abbruch ohne Fetch | Integrationstests; Datei-/Config-Erhalt |
| Autorisierter Fast-Forward | Integrationstest mit fortgeschrittenem Bare-Remote |
| Dirty, ignorierte Dateien, divergent, detached, kein Upstream, index.lock, unterbrochener Merge | Update blockiert; lokale Dateien bytegleich |
| Fremdrepo/belegter Nicht-Repo-Ordner | Blockierung; Bytevergleich |
| Symlink/Junction und verschachteltes/external Git-Verzeichnis | Windows-Junction-/plattformnative Symlinktests |
| Ziel wird während Clone belegt; Änderung während Bestätigungsdialog | erneute Prüfung, keine Überschreibung |
| Git fehlt, Anmeldung/Zugriff fehlt, Netzwerk-/Clone-/Rechtefehler | injizierte Prozessfehler; keine Erfolgszuordnung oder Secret-Ausgabe |
| Abbruch samt Kindprozess | echter Git-Kindprozess und abgebrochener Clone |
| Leer-Repository, Status/Pull/Commit/Push | lokale Bare-Remotes; erster Commit/Upstream |
| Persistenz, Speicherfehler, vorherige Zuordnung | Store-/ViewModel-Tests |
| DE/EN und Erweiterbarkeit | Schlüssel-/Format-/Fehlerabdeckung, Fallback |
| Tatsächlich privates GitHub-Repo | opt-in `tools/ImportSmoke`: Produktionsdienste, Clone/Link/Status/Safe Pull, keine Remote-Schreiboperation |
| Windows/macOS | CI-Matrix im Produktrepository; Ergebnisse im jeweiligen PR prüfen |

Lokaler Nachweis vom 2026-10-28: Windows, .NET SDK 10.0.403, Ziel `net8.0`; Release-Build ohne Warnungen/Fehler und **316/316 Tests PASS**. Echter privater Clone/Link/Status/Safe Pull über SSH-origin und unveränderte globale Git-Konfiguration ebenfalls PASS. Windows-UI-Smoke: neue Aktion und Zielauswahldialog beobachtet. CI-/macOS- und finale Source-Revision siehe verknüpften Produkt-PR und dessen Prüfbericht; kein behaupteter macOS-Finder-/Installer-Nachweis allein aus dieser Spezifikation.

## Aussagegrenzen und spätere Erweiterungen

Portable .NET-Pfadprüfungen minimieren TOCTOU; vollständige handlebasierte Isolation gegen einen bösartigen Prozess mit denselben Dateirechten wird nicht behauptet. Während des Imports darf ein anderer Prozess den Zielbaum nicht aktiv austauschen.

Git/gh, vorhandene Git-Konfiguration und systemweite Checkout-Filter sind lokale Vertrauensgrundlagen. Keine Builds oder Repository-Hooks im Import. Submodule werden nicht rekursiv initialisiert; LFS benötigt eine bestehende Git-LFS-Installation. Externe Worktrees und GitHub Enterprise erfordern einen eigenen Vertrag. Temporäre Clones können private Inhalte und belegten Speicher enthalten; keine ungefragte Bereinigung.

## Änderungslog

- **1.0.0 · 2026-10-28:** initialer versionierter Vertrag und Referenzzuordnung für Clone, Link, kontrolliertes Update, Credential-Scope, Dateischutz, Persistenz, Lokalisierung und Testmatrix. Spezifikation im Review; keine implizite Produktintegration oder Releasefreigabe.
