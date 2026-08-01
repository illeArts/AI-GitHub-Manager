# Release-QA-Abnahme — AI GitHub Manager v{VERSION}

Verbindliche, 1:1 abhakbare Release-Abnahme für Version {VERSION}. Jeder Test hat einen
Schritt-für-Schritt-Ablauf, ein erwartetes Ergebnis, ein eindeutiges Abbruchkriterium
(FAIL) und eine ungefähre Dauer. Screenshots werden nur dort verlangt, wo sie einen
Fehler eindeutig dokumentieren — nicht als Nachweis für erfolgreiche Schritte.

> **So verwendest du diese Vorlage:** Kopiere diese Datei nach
> `docs/testing/RELEASE_QA_v{VERSION}.md`, ersetze alle `{PLATZHALTER}` durch die
> konkreten Werte des Releases, und passe Blöcke inhaltlich an die tatsächlich in
> dieser Version enthaltenen Änderungen an (Zeilen ergänzen/entfernen, wo nötig).
> Trage das Ergebnis danach als neue Zeile in `docs/testing/RELEASE_HISTORY.md` ein.
> Prüfe bei jeder neuen Version außerdem, ob ältere, versionsspezifische Hinweise
> (z. B. „Bekannte Einschränkungen") noch zutreffen, statt sie unreflektiert aus der
> Vorversion zu übernehmen — sie sollten explizit als temporär markiert sein, falls
> sie das sind.

**Geltungsbereich:** Diese Abnahme prüft den Stand von PR #{PR_NUMBER} (Commits
`{COMMIT_LIST}`) auf Branch `{BRANCH_NAME}` vor dem Merge nach `main`.

**Getestet von:** _______________  **Datum:** _______________  **Plattform(en):** _______________

**Verwaltung:** Diese Datei wird für jede Version neu aus dieser Vorlage erzeugt und
unter `docs/testing/RELEASE_QA_v{VERSION}.md` abgelegt. Ältere Versionen bleiben
unverändert im Repository erhalten (Historie, siehe `RELEASE_HISTORY.md`).

---

## ⚠️ Bekannte Einschränkungen vor Testbeginn (falls vorhanden)

> Trage hier versionsspezifische, bereits bekannte technische Lücken ein, die die
> Bewertung einzelner Tests beeinflussen (z. B. „Feature X ist für diese Version noch
> nicht auf allen Plattformen verfügbar — betroffene Tests gelten als BLOCKED, nicht
> als FAIL"). Markiere jeden Punkt ausdrücklich mit seiner Gültigkeit
> (z. B. „nur v{VERSION}") und einer Bedingung, wann er entfällt. Wenn es für diese
> Version keine bekannten Einschränkungen gibt, diesen Abschnitt vollständig
> entfernen statt ihn leer stehen zu lassen.

---

## Block 1 — Windows Installer

**Voraussetzung:** `{INSTALLER_FILENAME}` liegt vor (lokal gebaut über den App-Button
„📦 Installer erstellen" oder aus dem CI-Artefakt). Eine ältere Version
(z. B. {PREVIOUS_VERSION}) ist bereits installiert.

### 1.1 Installation über bestehende Version (Update-Installation)
**Dauer:** ~3 Min.
1. Bestehende Installation ({PREVIOUS_VERSION} o. ä.) prüfen: Startmenü → AI GitHub Manager öffnen, Version im „Über"-Fenster notieren.
2. `{INSTALLER_FILENAME}` ausführen.
3. Installationsassistenten mit Standardwerten durchklicken (Verzeichnis nicht ändern).
4. Installation abschließen lassen.

**Erwartetes Ergebnis:** Installer läuft ohne Fehlermeldung durch, erkennt die
bestehende Installation und überschreibt sie in-place (kein manuelles Deinstallieren
nötig), Fortschrittsanzeige erreicht 100 %.

**FAIL-Kriterium:** Installer bricht mit Fehlermeldung ab, verlangt manuelle
Deinstallation, oder installiert in ein zweites, paralleles Verzeichnis.
→ **Screenshot der Fehlermeldung erforderlich.**

### 1.2 Version prüfen nach Update-Installation
**Dauer:** ~1 Min.
1. App aus dem Startmenü/Desktop-Icon starten.
2. Menü **Über** öffnen.
3. Versionsnummer am unteren Rand des Über-Fensters ablesen.

**Erwartetes Ergebnis:** Anzeige lautet exakt `v{VERSION}` (Format
`v{Major}.{Minor}.{Build}`). Logo und Versionsnummer sind vollständig sichtbar, nicht
abgeschnitten.

**FAIL-Kriterium:** Angezeigte Version ≠ `v{VERSION}`, ODER Versionsnummer/Logo werden
am unteren Fensterrand abgeschnitten/verdeckt. → **Screenshot erforderlich.**

### 1.3 Programmstart prüfen
**Dauer:** ~2 Min.
1. App vollständig schließen.
2. Über Desktop-Verknüpfung erneut starten.
3. Über Startmenü-Eintrag erneut starten.
4. Hauptfenster auf Vollständigkeit prüfen: Projektliste (links), Ausgabefeld (Mitte), Aktionen-Panel (rechts).

**Erwartetes Ergebnis:** App startet in beiden Fällen ohne Absturz/Exception-Dialog,
Hauptfenster zeigt alle drei Bereiche korrekt an, Taskbar-/Fenstericon zeigt das
aktuelle Logo.

**FAIL-Kriterium:** App startet nicht, zeigt eine .NET-Exception, oder ein Bereich des
Hauptfensters fehlt/ist leer. → **Screenshot erforderlich.**

### 1.4 Deinstallation
**Dauer:** ~2 Min.
1. Windows-Einstellungen → Apps → „AI GitHub Manager" → Deinstallieren (oder über den
   Startmenü-Eintrag „Uninstall AI GitHub Manager").
2. Deinstallation bestätigen und abwarten.
3. Installationsverzeichnis und `%APPDATA%\AI.GitHubManager` (Settings-Datei) manuell prüfen.

**Erwartetes Ergebnis:** Deinstallation läuft ohne Fehler durch, Programmdateien
werden entfernt, Einstellungsordner wird laut `[UninstallDelete]` in `setup.iss`
ebenfalls entfernt.

**FAIL-Kriterium:** Deinstallation schlägt fehl, Programmverzeichnis bleibt mit
Programmdateien zurück, oder die Deinstallation hinterlässt Fehlermeldungen.
→ **Screenshot erforderlich.**

### 1.5 Neuinstallation (Clean Install)
**Dauer:** ~3 Min.
1. `{INSTALLER_FILENAME}` auf einem System ohne vorherige Installation ausführen
   (oder nach vollständiger Deinstallation aus 1.4).
2. Standard-Installationspfad übernehmen.
3. Nach Abschluss direkt starten lassen (Installer-Option „Programm starten").

**Erwartetes Ergebnis:** Saubere Neuinstallation ohne Fehler, App startet
automatisch, zeigt leere/erste Projektliste (kein Zustand aus vorheriger Installation
sichtbar, sofern Settings-Datei zuvor entfernt wurde).

**FAIL-Kriterium:** Installation schlägt fehl, oder die App startet mit
unerwarteten Altdaten trotz vorheriger vollständiger Deinstallation.
→ **Screenshot erforderlich.**

---

## Block 2 — Plattformabhängiges Update

**Voraussetzung:** Ein GitHub Release mit Tag `v{VERSION}` (oder höher, für einen
Vorab-Test) existiert mit den erwarteten Assets.

Button: **„Auf Updates prüfen"** (`CheckForUpdateCommand`).

### 2.1 Windows
**Dauer:** ~2 Min.
1. Auf einem Windows-System „Auf Updates prüfen" klicken.
2. Banner und Log-Ausgabe prüfen.

**Erwartetes Ergebnis:** Ist eine neuere Version veröffentlicht, erscheint das Banner
„⬆ Update verfügbar: v{Version} (aktuell: v{Version})" und der Download-Button bietet
**ausschließlich** die Windows-`.exe` an — niemals ein `.zip`, `.tar.gz` oder `.dmg`.
Ist keine neuere Version vorhanden: Log zeigt „App ist aktuell (v{Version})."

**FAIL-Kriterium:** Der angebotene Download ist keine `win-x64.exe`-Datei, oder das
Banner bleibt trotz vorhandenem neueren Release aus. → **Screenshot erforderlich.**

### 2.2 macOS Intel
**Dauer:** ~2 Min.
1. Auf einem Mac mit Intel-CPU „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis:** Angebotener Download ist ausschließlich das macOS-Intel-Paket
für diese Version.

**FAIL-Kriterium:** Ein anderes Paketformat wird angeboten, oder kein Download trotz
vorhandenem, korrekt benanntem Release-Asset. → **Screenshot erforderlich.**

### 2.3 macOS Apple Silicon
**Dauer:** ~2 Min.
1. Auf einem Mac mit Apple-Silicon-CPU „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis:** Angebotener Download ist ausschließlich das macOS-ARM64-Paket
für diese Version.

**FAIL-Kriterium:** Wie 2.2.

### 2.4 Linux
**Dauer:** ~2 Min.
1. Auf einem Linux-x64-System „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis:** Angebotener Download ist ausschließlich das Linux-x64-Paket
für diese Version.

**FAIL-Kriterium:** Wie 2.2.

### 2.5 Fehlendes Asset (beliebige Plattform ohne passendes Paket)
**Dauer:** ~3 Min.
1. Auf einer Plattform testen, für die im geprüften Release kein passendes Asset
   existiert (z. B. testweise ein Asset entfernen).
2. „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis:** Banner „⬆ Update verfügbar: v{Version} (kein passendes
Paket)", Log erklärt verständlich, dass kein passendes Paket verfügbar ist, und die
Release-Seite wird automatisch im Standardbrowser geöffnet. **Es wird niemals ein
falsches Asset heruntergeladen oder angeboten.**

**FAIL-Kriterium:** Die App bietet irgendein Asset an, das nicht zur aktuellen
Plattform passt, ODER es öffnet sich keine Release-Seite, ODER es erscheint eine
rohe Fehlermeldung/Exception statt der verständlichen Meldung.
→ **Screenshot erforderlich.**

---

## Block 3 — Safe Pull

**Voraussetzung:** Ein Test-Repository mit Schreibzugriff, Einstellung „Sicherer Pull
mit automatischer Schutzsicherung" ist aktiv (Standardeinstellung, Settings-Fenster).

### Szenario A — Lokale Änderung an bereits getrackter Datei
**Dauer:** ~5 Min.
1. Projekt in der App auswählen, sicherstellen, dass der Arbeitsbaum sauber ist.
2. Eine bereits im Repository vorhandene Datei lokal ändern, **nicht committen**.
3. Auf einem zweiten Klon/Remote eine neue Commit-Änderung pushen, damit der lokale
   Branch hinter dem Remote zurückliegt.
4. In der App **Pull** klicken.

**Erwartetes Ergebnis (Reihenfolge wie im Log sichtbar):**
   a. Schutzsicherung wird automatisch erstellt (`git stash push --include-untracked`
      mit eindeutiger Kennung).
   b. `git pull --ff-only` läuft (kein automatischer Merge/Rebase).
   c. Nach erfolgreichem Pull wird die Schutzsicherung automatisch wiederhergestellt
      und bei verifiziert konfliktfreier Wiederherstellung gelöscht.
   d. Die lokale Änderung ist danach unverändert vorhanden, UND die Remote-Änderung
      ist ebenfalls vorhanden.
   e. `git stash list` zeigt danach keinen verbleibenden Eintrag der eigenen
      Schutzsicherung.

**FAIL-Kriterium:** Die lokale Änderung ist nach dem Pull verschwunden oder
unvollständig, ODER die Remote-Änderung fehlt, ODER ein `git reset --hard` wurde
sichtbar ausgeführt, ODER die Schutzsicherung bleibt unnötig als Stash-Leiche
zurück. → **Screenshot der Log-Ausgabe UND des betroffenen Datei-Diffs
erforderlich.**

### Szenario B — Lokale Änderung + ungetrackte Datei gleichzeitig
**Dauer:** ~5 Min.
1. Wie Szenario A, Schritt 1–3, zusätzlich:
2. Eine bereits getrackte Datei lokal ändern **und** eine komplett neue, ungetrackte
   Datei anlegen.
3. In der App **Pull** klicken.

**Erwartetes Ergebnis:** Sowohl die Änderung an der getrackten Datei als auch die
neue ungetrackte Datei sind nach dem Pull vollständig vorhanden. Die Remote-Änderung
ist ebenfalls eingepflegt.

**FAIL-Kriterium:** Die ungetrackte Datei fehlt nach dem Pull, ODER ihr Inhalt wurde
verändert, ODER die getrackte Änderung ist verloren gegangen.
→ **Screenshot erforderlich.**

---

## Block 4 — `.git/index.lock`

**Voraussetzung:** Testrepository wie in Block 3. Für Fall 1/2 wird die Datei
`.git/index.lock` manuell angelegt, um den realen Fehler gezielt zu reproduzieren:

```text
error: Unable to create '.git/index.lock': File exists.
Another git process seems to be running in this repository, or the lock file may be stale.
```

### Fall 1 — Verwaister Lock
**Dauer:** ~5 Min.
1. `.git/index.lock` manuell anlegen (leere Datei genügt).
2. Sicherstellen, dass **kein** echter Git-Prozess mehr läuft (alle Terminals/IDEs mit
   offenem Git-Zugriff auf dieses Repository schließen).
3. Mindestens 10 Sekunden warten.
4. In der App **Umgebung prüfen** klicken.
5. Button **„🔓 Verwaiste Git-Sperre sicher entfernen"** sollte jetzt sichtbar sein
   → klicken.
6. Anschließend **Pull** klicken.

**Erwartetes Ergebnis:** Button wird nach Schritt 4 sichtbar. Nach Klick in Schritt 5
Meldung, dass die Sperre sicher entfernt wurde und `git status` einen gültigen
Zustand zeigt. `.git/index.lock` ist danach vom Dateisystem entfernt. Pull in
Schritt 6 läuft erfolgreich wie in Block 3 beschrieben.

**FAIL-Kriterium:** Der Button erscheint nicht, obwohl kein Prozess aktiv ist und die
Wartezeit eingehalten wurde, ODER die Sperre wird nicht entfernt, ODER `git status`
nach der Entfernung meldet einen Fehler, ODER der anschließende Pull schlägt fehl.
→ **Screenshot erforderlich.**

### Fall 2 — Aktiver Git-Prozess
**Dauer:** ~5 Min.
1. `.git/index.lock` manuell anlegen.
2. Bewusst einen echten, lang laufenden Git-Prozess für dasselbe Repository aktiv
   halten (z. B. `git gc` in einem Terminal starten und laufen lassen).
3. In der App **Umgebung prüfen** klicken, danach **Pull** klicken.

**Erwartetes Ergebnis:** Der Button „Verwaiste Git-Sperre sicher entfernen"
**erscheint nicht**. Ein Pull-Versuch wird mit einer verständlichen Meldung
abgebrochen — **bevor** irgendein Stash erstellt oder eine Datei verändert wird.
`.git/index.lock` bleibt unverändert bestehen.

**FAIL-Kriterium:** Die Sperre wird trotz laufendem Git-Prozess entfernt, ODER es
wird dennoch ein Stash erstellt/ein Pull ausgeführt, ODER die Abbruchmeldung fehlt
oder ist irreführend. → **Screenshot erforderlich — dies ist der kritischste
Einzeltest der gesamten Abnahme (Datenverlustrisiko).**

### Fall 3 — Unterbrochener Merge (MERGE_HEAD)
**Dauer:** ~5 Min.
1. Einen echten Merge-Konflikt provozieren und den Merge-Vorgang bewusst
   unterbrochen/unaufgelöst stehen lassen, sodass `.git/MERGE_HEAD` existiert.
2. In der App **Umgebung prüfen** klicken.
3. **Pull** klicken.

**Erwartetes Ergebnis:** Log zeigt eine verständliche Warnung, dass ein Merge
unterbrochen ist und manuell abgeschlossen/abgebrochen werden muss. Ein Pull-Versuch
wird sofort abgebrochen, **ohne** dass `MERGE_HEAD` oder sonstige Merge-Zustände
automatisch verändert/gelöscht werden.

**FAIL-Kriterium:** Die App versucht, den unterbrochenen Merge automatisch zu
bereinigen oder zu ignorieren, ODER `MERGE_HEAD` wird durch die App entfernt, ODER
die Warnung erscheint nicht. → **Screenshot erforderlich.**

---

## Block 5 — Regression

Kurzer Rundgang durch die Kernfunktionen, um sicherzustellen, dass keine der
Änderungen dieser Version bestehendes Verhalten beschädigt hat.

| # | Aktion | Erwartetes Ergebnis | Dauer |
|---|---|---|---|
| 5.1 | **Clone** — Repository per „Von GitHub importieren" oder manuellem `git clone` + „+ Hinzufügen" einbinden | Projekt erscheint korrekt in der Liste, lokaler Pfad korrekt zugeordnet | ~3 Min. |
| 5.2 | **Commit** — Datei ändern, „Commit + Push" klicken | Änderung wird committet und gepusht, Log zeigt Erfolg | ~2 Min. |
| 5.3 | **Push** — erneuter Push ohne Änderungen | Log meldet sinngemäß „Keine Änderungen und keine ungesendeten Commits", kein Fehler | ~1 Min. |
| 5.4 | **Branch wechseln** — lokal per Terminal, danach in der App „Umgebung prüfen"/„Status" | App zeigt den neuen Branch korrekt an, keine falschen Zustände | ~2 Min. |
| 5.5 | **Repository wechseln** — anderes Projekt aus der Liste auswählen | Ausgabefeld, Pfad, Branch aktualisieren sich korrekt auf das neue Projekt; keine Vermischung mit dem vorherigen Repository | ~2 Min. |
| 5.6 | **Release erstellen** — Button „📦 Installer erstellen" ausführen | Installer wird erzeugt (oder verständlicher Hinweis, falls eine Voraussetzung fehlt) | ~5 Min. |
| 5.7 | **Settings** — Einstellungsfenster öffnen, Optionen umschalten, Fenster schließen und erneut öffnen | Auswahl bleibt nach Neustart des Fensters/der App erhalten | ~2 Min. |
| 5.8 | **About** — Über-Fenster öffnen | Zeigt `v{VERSION}`, Logo und Text vollständig ohne Abschneiden | ~1 Min. |
| 5.9 | **Update** — „Auf Updates prüfen" ohne vorhandenes neueres Release | Log meldet „App ist aktuell (v{VERSION})."; kein falsches Banner | ~1 Min. |
| 5.10 | **Repository löschen** — „− Entfernen" für ein Projekt in der Liste klicken | Projekt verschwindet sauber aus der Liste; lokale Projektdateien auf der Festplatte bleiben in jedem Fall unangetastet | ~2 Min. |
| 5.11 | **Update nach Update** — nach einem erfolgreich durchgeführten Update erneut „Auf Updates prüfen" klicken | Log meldet „App ist aktuell (v{neue Version})."; kein erneutes „Update verfügbar"-Banner für die soeben installierte Version | ~1 Min. |

**FAIL-Kriterium (gesamter Block):** Irgendeine Zeile weicht vom erwarteten Ergebnis
ab. → **Screenshot der jeweils abweichenden Zeile erforderlich.**

---

## Abschluss

| Test | Ergebnis | GO/FAIL |
|------|----------|---------|
| 1.1 Installation über bestehende Version | | |
| 1.2 Version prüfen | | |
| 1.3 Programmstart prüfen | | |
| 1.4 Deinstallation | | |
| 1.5 Neuinstallation | | |
| 2.1 Update — Windows | | |
| 2.2 Update — macOS Intel | | |
| 2.3 Update — macOS Apple Silicon | | |
| 2.4 Update — Linux | | |
| 2.5 Update — fehlendes Asset | | |
| 3.A Safe Pull — lokale Änderung | | |
| 3.B Safe Pull — Änderung + ungetrackte Datei | | |
| 4.1 index.lock — verwaist | | |
| 4.2 index.lock — aktiver Prozess | | |
| 4.3 index.lock — MERGE_HEAD | | |
| 5.1–5.11 Regression | | |

**Bewertungsregel:** Für ein **GO** müssen alle Zeilen mit GO bewertet sein (außer
ausdrücklich als BLOCKED markierte Zeilen, siehe „Bekannte Einschränkungen" oben,
falls vorhanden). Ein einziges FAIL in Block 4 (insbesondere 4.2) führt automatisch
zu **NO GO**, unabhängig vom Ergebnis der übrigen Blöcke, da hier reales
Datenverlustrisiko besteht.

```text
Release Candidate
GO / NO GO   (Zutreffendes eintragen)
```

**Begründung:**

_______________________________________________________________

_______________________________________________________________

**Freigabe** (bewusst getrennt: technisch bestanden ≠ für Release freigegeben):

```text
Technische Freigabe:
_____________________

Produktfreigabe:
_____________________

Datum:
_____________________
```

**Falls NO GO:** Konkrete fehlgeschlagene Tests, betroffene Commits/Dateien und
nächste Schritte hier auflisten, bevor ein erneuter Anlauf gestartet wird.
