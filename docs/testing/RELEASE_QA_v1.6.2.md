# Release-QA-Abnahme — AI GitHub Manager v1.6.2

Verbindliche, 1:1 abhakbare Release-Abnahme für Version 1.6.2. Jeder Test hat einen
Schritt-für-Schritt-Ablauf, ein erwartetes Ergebnis, ein eindeutiges Abbruchkriterium
(FAIL) und eine ungefähre Dauer. Screenshots werden nur dort verlangt, wo sie einen
Fehler eindeutig dokumentieren — nicht als Nachweis für erfolgreiche Schritte.

**Geltungsbereich:** Diese Abnahme prüft den Stand von PR #5 (Commits `fe5a8ae` +
`9c3e20c`) auf Branch `fix/v1.6.2-platform-update-safe-pull` vor dem Merge nach `main`.

**Getestet von:** _______________  **Datum:** _______________  **Plattform(en):** _______________

**Verwaltung:** Diese Datei wird ab v1.6.3 fortgeschrieben (Versionsnummer im Dateinamen
und in dieser Kopfzeile anpassen, Blöcke bei Bedarf ergänzen). Sie ist Teil des
Repositories unter `docs/testing/RELEASE_QA_v1.6.2.md`.

---

## ⚠️ Bekannte Einschränkung vor Testbeginn (Block 2, macOS)

Der aktuelle Release-Workflow (`.github/workflows/release-v1.6.2.yml`, Job
`portable-packages`) baut und veröffentlicht **nur** `windows-x64` und `linux-x64` —
es gibt aktuell **keinen** CI-Job, der ein macOS-Paket erzeugt oder hochlädt. Das
manuelle Skript `build-installer-mac.sh` erzeugt zudem `.dmg`-Dateien nach dem Muster
`AI_GitHub_Manager_<Version>_macOS_<arm64|x64>.dmg` — das entspricht **nicht** dem
Namensmuster, das `ReleaseAssetSelector.cs` erwartet (`macos-x64.zip` /
`macos-arm64.zip`). Würde ein `.dmg` unverändert an ein Release angehängt, würde die
App auf macOS in den "kein passendes Paket"-Zweig laufen, nicht in den `.dmg` finden.

**Konsequenz für diese Abnahme:** Die macOS-Teiltests in Block 2 können erst
sinnvoll ausgeführt werden, wenn entweder (a) ein CI-Job `macos-x64.zip` /
`macos-arm64.zip` erzeugt, oder (b) `build-installer-mac.sh` auf das erwartete
Zip-Namensschema umgestellt wird, oder (c) `ReleaseAssetSelector` bewusst erweitert
wird, `.dmg` zu akzeptieren. Bis eine dieser drei Optionen umgesetzt ist, gilt Block 2
für macOS als **BLOCKED**, nicht als bestanden oder fehlgeschlagen — das GO für
Windows/Linux ist davon unabhängig zu bewerten (siehe Abschlusstabelle).

---

## Block 1 — Windows Installer

**Voraussetzung:** `AI_GitHub_Manager_Setup_1.6.2_win-x64.exe` liegt vor (lokal gebaut
über den App-Button „📦 Installer erstellen" oder aus dem CI-Artefakt
`artifacts/installer/AI_GitHub_Manager_Setup_1.6.2_win-x64.exe`). Eine ältere Version
(z. B. 1.6.1) ist bereits installiert.

### 1.1 Installation über bestehende Version (Update-Installation)
**Dauer:** ~3 Min.
1. Bestehende Installation (v1.6.1 o. ä.) prüfen: Startmenü → AI GitHub Manager öffnen, Version im „Über"-Fenster notieren.
2. `AI_GitHub_Manager_Setup_1.6.2_win-x64.exe` ausführen.
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
2. Menü **Über** öffnen (bzw. entsprechenden Button/Menüpunkt).
3. Versionsnummer am unteren Rand des Über-Fensters ablesen.

**Erwartetes Ergebnis:** Anzeige lautet exakt `v1.6.2` (kleines „v", Format
`v{Major}.{Minor}.{Build}`). Logo und Versionsnummer sind vollständig sichtbar,
nicht abgeschnitten (siehe Fix in Commit `fe5a8ae`: Logo 260px→130px, Fenster
620px→660px).

**FAIL-Kriterium:** Angezeigte Version ≠ `v1.6.2`, ODER Versionsnummer/Logo werden am
unteren Fensterrand abgeschnitten/verdeckt. → **Screenshot erforderlich.**

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
3. Installationsverzeichnis (`%ProgramFiles%\AI GitHub Manager` bzw. das gewählte
   Verzeichnis) und `%APPDATA%\AI.GitHubManager` (Settings-Datei) manuell prüfen.

**Erwartetes Ergebnis:** Deinstallation läuft ohne Fehler durch, Programmdateien
werden entfernt, `%APPDATA%\AI.GitHubManager`-Einstellungsordner wird laut
`[UninstallDelete]` in `setup.iss` ebenfalls entfernt.

**FAIL-Kriterium:** Deinstallation schlägt fehl, Programmverzeichnis bleibt mit
Programmdateien zurück, oder die Deinstallation hinterlässt Fehlermeldungen.
→ **Screenshot erforderlich.**

### 1.5 Neuinstallation (Clean Install)
**Dauer:** ~3 Min.
1. `AI_GitHub_Manager_Setup_1.6.2_win-x64.exe` auf einem System ohne vorherige
   Installation ausführen (oder nach vollständiger Deinstallation aus 1.4).
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

**Voraussetzung:** Ein GitHub Release mit Tag ≥ `v1.6.2` (oder ein Test-Release mit
höherer Versionsnummer) existiert mit den erwarteten Assets. Für den „Fehlendes
Asset"-Test wird ein Release ohne passendes Paket für die jeweilige Plattform
benötigt (z. B. testweise ein Asset entfernen oder ein Release ohne Linux-Paket
verwenden).

Button: **„Auf Updates prüfen"** (`CheckForUpdateCommand`).

### 2.1 Windows
**Dauer:** ~2 Min.
1. Auf einem Windows-System mit installierter v1.6.2 (oder einer älteren Version, um
   ein „Update verfügbar"-Banner zu erzwingen) „Auf Updates prüfen" klicken.
2. Banner und Log-Ausgabe prüfen.

**Erwartetes Ergebnis:** Ist eine neuere Version veröffentlicht, erscheint das Banner
„⬆ Update verfügbar: v{Version} (aktuell: v{Version})" und der Button „⬇ Jetzt
herunterladen" bietet **ausschließlich** `AI_GitHub_Manager_Setup_<Version>_win-x64.exe`
zum Download an — niemals ein `.zip`, `.tar.gz` oder `.dmg`. Ist keine neuere Version
vorhanden: Log zeigt „App ist aktuell (v{Version})."

**FAIL-Kriterium:** Der angebotene Download ist keine `win-x64.exe`-Datei (z. B.
versehentlich das Linux- oder macOS-Paket), oder das Banner bleibt trotz vorhandenem
neueren Release aus. → **Screenshot erforderlich.**

### 2.2 macOS Intel — **BLOCKED, siehe Hinweis oben**
**Dauer:** ~2 Min. (sobald entblockt)
1. Auf einem Mac mit Intel-CPU „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis (sobald das CI-Asset existiert):** Angebotener Download ist
ausschließlich `AI-GitHub-Manager-v<Version>-macos-x64.zip`.

**Status für v1.6.2:** Nicht testbar, da kein passendes Release-Asset existiert
(siehe Einschränkung oben). Als „BLOCKED" markieren, nicht als FAIL — außer die App
zeigt fälschlich ein falsches (z. B. Windows-)Paket an, statt „kein passendes Paket"
zu melden; das wäre ein echtes FAIL. → **Screenshot erforderlich, falls falsches
Asset angeboten wird.**

### 2.3 macOS Apple Silicon — **BLOCKED, siehe Hinweis oben**
**Dauer:** ~2 Min. (sobald entblockt)
1. Auf einem Mac mit Apple-Silicon-CPU „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis (sobald das CI-Asset existiert):** Angebotener Download ist
ausschließlich `AI-GitHub-Manager-v<Version>-macos-arm64.zip`.

**Status für v1.6.2:** Wie 2.2 — BLOCKED bis ein CI-Asset mit passendem Namen
existiert. Gleiches FAIL-Kriterium wie 2.2.

### 2.4 Linux
**Dauer:** ~2 Min.
1. Auf einem Linux-x64-System „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis:** Angebotener Download ist ausschließlich
`AI-GitHub-Manager-v<Version>-linux-x64.tar.gz`.

**FAIL-Kriterium:** Ein anderes Paketformat wird angeboten, oder kein Download trotz
vorhandenem, korrekt benanntem Release-Asset. → **Screenshot erforderlich.**

### 2.5 Fehlendes Asset (beliebige Plattform ohne passendes Paket)
**Dauer:** ~3 Min.
1. Auf einer Plattform testen, für die im geprüften Release kein passendes Asset
   existiert (aktuell: jede macOS-Variante, siehe 2.2/2.3 — oder testweise ein
   Release ohne Linux-Paket erstellen).
2. „Auf Updates prüfen" klicken.

**Erwartetes Ergebnis:** Banner „⬆ Update verfügbar: v{Version} (kein passendes
Paket)", Log erklärt verständlich, dass für diese Plattform/Architektur kein
passendes Paket verfügbar ist, und die Release-Seite (`html_url`) wird automatisch im
Standardbrowser geöffnet. **Es wird niemals ein falsches Asset (z. B. die
Windows-.exe auf macOS) heruntergeladen oder angeboten.**

**FAIL-Kriterium:** Die App bietet irgendein Asset an, das nicht zur aktuellen
Plattform passt, ODER es öffnet sich keine Release-Seite, ODER es erscheint eine
rohe Fehlermeldung/Exception statt der verständlichen Meldung.
→ **Screenshot erforderlich.**

---

## Block 3 — Safe Pull

**Voraussetzung:** Ein Test-Repository mit Schreibzugriff, Einstellung „Sicherer Pull
mit automatischer Schutzsicherung" ist aktiv (Standardeinstellung, Settings-Fenster,
Radiobutton `SafePullOn`).

### Szenario A — Lokale Änderung an bereits getrackter Datei
**Dauer:** ~5 Min.
1. Projekt in der App auswählen, sicherstellen, dass der Arbeitsbaum sauber ist
   (`git status` lokal prüfen).
2. Eine bereits im Repository vorhandene Datei lokal ändern (z. B. eine Zeile in
   `README.md` anhängen), **nicht committen**.
3. Auf einem zweiten Klon/Remote eine neue Commit-Änderung pushen, damit der lokale
   Branch hinter dem Remote zurückliegt.
4. In der App **Pull** klicken.

**Erwartetes Ergebnis (Reihenfolge wie im Log sichtbar):**
   a. Schutzsicherung wird automatisch erstellt (`git stash push --include-untracked`
      mit eindeutiger Kennung „AI GitHub Manager auto-backup …").
   b. `git pull --ff-only` läuft (kein automatischer Merge/Rebase).
   c. Nach erfolgreichem Pull wird die Schutzsicherung automatisch per
      `git stash apply` wiederhergestellt und bei verifiziert konfliktfreier
      Wiederherstellung gelöscht.
   d. Die lokale Änderung aus Schritt 2 ist danach unverändert im Arbeitsbaum
      vorhanden, UND die neue Remote-Änderung aus Schritt 3 ist ebenfalls vorhanden.
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
   Datei anlegen (z. B. `scratch-test.txt` mit beliebigem Inhalt).
3. In der App **Pull** klicken.

**Erwartetes Ergebnis:** Sowohl die Änderung an der getrackten Datei als auch die
neue ungetrackte Datei sind nach dem Pull vollständig vorhanden (Schutzsicherung
läuft mit `--include-untracked`). Die Remote-Änderung ist ebenfalls eingepflegt.

**FAIL-Kriterium:** Die ungetrackte Datei fehlt nach dem Pull, ODER ihr Inhalt wurde
verändert, ODER die getrackte Änderung ist verloren gegangen.
→ **Screenshot erforderlich.**

---

## Block 4 — `.git/index.lock`

**Voraussetzung:** Testrepository wie in Block 3. Für Fall 1/2 wird die Datei
`.git/index.lock` manuell angelegt (z. B. `New-Item -ItemType File
.git\index.lock` unter Windows), um den realen Fehler gezielt zu reproduzieren:

```text
error: Unable to create '.git/index.lock': File exists.
Another git process seems to be running in this repository, or the lock file may be stale.
```

### Fall 1 — Verwaister Lock
**Dauer:** ~5 Min.
1. `.git\index.lock` manuell anlegen (leere Datei genügt).
2. Sicherstellen, dass **kein** echter Git-Prozess mehr läuft (alle Terminals/IDEs mit
   offenem Git-Zugriff auf dieses Repository schließen).
3. Mindestens 10 Sekunden warten (Sicherheitsschwelle `MinimumOrphanAge` = 5 Sekunden
   in `GitLockGuard.cs`, mit Puffer testen).
4. In der App **Umgebung prüfen** klicken.
5. Button **„🔓 Verwaiste Git-Sperre sicher entfernen"** sollte jetzt sichtbar sein
   → klicken.
6. Anschließend **Pull** klicken.

**Erwartetes Ergebnis:**
   - Nach Schritt 4: Button aus Schritt 5 wird sichtbar (Property
     `CanRemoveOrphanedGitLock = true`).
   - Nach Klick in Schritt 5: Meldung „✅ Die verwaiste Git-Sperre wurde sicher
     entfernt; 'git status' zeigt danach einen gültigen Repository-Zustand." Die
     Datei `.git/index.lock` ist danach vom Dateisystem entfernt.
   - Pull in Schritt 6 läuft erfolgreich wie in Block 3 beschrieben.

**FAIL-Kriterium:** Der Button erscheint nicht, obwohl kein Prozess aktiv ist und die
Wartezeit eingehalten wurde, ODER die Sperre wird nicht entfernt, ODER `git status`
nach der Entfernung meldet einen Fehler, ODER der anschließende Pull schlägt fehl.
→ **Screenshot erforderlich.**

### Fall 2 — Aktiver Git-Prozess
**Dauer:** ~5 Min.
1. `.git\index.lock` manuell anlegen.
2. Bewusst einen echten, lang laufenden Git-Prozess für dasselbe Repository aktiv
   halten (z. B. `git gc` in einem Terminal starten und laufen lassen, oder ein
   Terminalfenster mit `git status` in einer Schleife offen halten — Hauptsache:
   ein Prozess namens `git`/`git.exe` läuft tatsächlich, während der Test läuft).
3. In der App **Umgebung prüfen** klicken, danach **Pull** klicken.

**Erwartetes Ergebnis:** Der Button „Verwaiste Git-Sperre sicher entfernen"
**erscheint nicht** (`CanRemoveOrphanedGitLock = false`). Ein Pull-Versuch wird mit
der Meldung „⏳ Ein aktiver Git-Prozess wurde erkannt (oder konnte nicht sicher
ausgeschlossen werden). Die Sperre bleibt bestehen, bis dieser Prozess beendet ist.
Bitte kurz warten und erneut prüfen." abgebrochen — **bevor** irgendein Stash
erstellt oder eine Datei verändert wird. `.git/index.lock` bleibt unverändert
bestehen.

**FAIL-Kriterium:** Die Sperre wird trotz laufendem Git-Prozess entfernt, ODER es
wird dennoch ein Stash erstellt/ein Pull ausgeführt, ODER die Abbruchmeldung fehlt
oder ist irreführend. → **Screenshot erforderlich — dies ist der kritischste
Einzeltest der gesamten Abnahme (Datenverlustrisiko).**

### Fall 3 — Unterbrochener Merge (MERGE_HEAD)
**Dauer:** ~5 Min.
1. Einen echten Merge-Konflikt provozieren (z. B. zwei divergierende Branches mit
   Änderung derselben Zeile derselben Datei mergen) und den Merge-Vorgang bewusst
   unterbrochen/unaufgelöst stehen lassen, sodass `.git/MERGE_HEAD` existiert.
2. In der App **Umgebung prüfen** klicken.
3. **Pull** klicken.

**Erwartetes Ergebnis:** Log zeigt zusätzlich die Warnung „⚠ Ein Merge ist
unterbrochen (MERGE_HEAD vorhanden). Bitte manuell abschließen oder abbrechen (git
merge --abort). Dieser Zustand wird nicht automatisch bereinigt." Ein Pull-Versuch
wird mit „⛔ …" (derselbe Text) sofort abgebrochen, **ohne** dass `MERGE_HEAD` oder
sonstige Merge-Zustände automatisch verändert/gelöscht werden.

**FAIL-Kriterium:** Die App versucht, den unterbrochenen Merge automatisch zu
bereinigen oder zu ignorieren, ODER `MERGE_HEAD` wird durch die App entfernt, ODER
die Warnung erscheint nicht. → **Screenshot erforderlich.**

---

## Block 5 — Regression

Kurzer Rundgang durch die Kernfunktionen, um sicherzustellen, dass keine der
1.6.2-Änderungen bestehendes Verhalten beschädigt hat.

| # | Aktion | Erwartetes Ergebnis | Dauer |
|---|---|---|---|
| 5.1 | **Clone** — Repository per „Von GitHub importieren" oder manuellem `git clone` + „+ Hinzufügen" einbinden | Projekt erscheint korrekt in der Liste, lokaler Pfad korrekt zugeordnet | ~3 Min. |
| 5.2 | **Commit** — Datei ändern, „Commit + Push" klicken | Änderung wird committet und gepusht, Log zeigt Erfolg | ~2 Min. |
| 5.3 | **Push** — bereits in 5.2 enthalten; zusätzlich prüfen: erneuter Push ohne Änderungen | Log meldet sinngemäß „Keine Änderungen und keine ungesendeten Commits", kein Fehler | ~1 Min. |
| 5.4 | **Branch wechseln** — lokal per Terminal `git checkout <anderer-branch>`, danach in der App „Umgebung prüfen"/„Status" | App zeigt den neuen Branch korrekt an, keine falschen Zustände | ~2 Min. |
| 5.5 | **Repository wechseln** — anderes Projekt aus der Liste auswählen | Ausgabefeld, Pfad, Branch aktualisieren sich korrekt auf das neue Projekt; keine Vermischung mit dem vorherigen Repository | ~2 Min. |
| 5.6 | **Release erstellen** — Button „📦 Installer erstellen" für ein Testprojekt/dieses Repository ausführen | Installer wird erzeugt (oder verständlicher Hinweis, falls Inno Setup fehlt, inkl. sichtbarem Button „⬇ Inno Setup installieren" auf Windows) | ~5 Min. |
| 5.7 | **Settings** — Einstellungsfenster öffnen, „Sicherer Pull …" vs. „Bei lokalen Änderungen nur warnen …" umschalten, Fenster schließen und erneut öffnen | Auswahl bleibt nach Neustart des Fensters/der App erhalten | ~2 Min. |
| 5.8 | **About** — Über-Fenster öffnen | Zeigt `v1.6.2`, Logo und Text vollständig ohne Abschneiden (siehe Block 1.2) | ~1 Min. |
| 5.9 | **Update** — „Auf Updates prüfen" ohne vorhandenes neueres Release | Log meldet „App ist aktuell (v1.6.2)."; kein falsches Banner | ~1 Min. |

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
| 2.2 Update — macOS Intel | | BLOCKED (siehe Hinweis) |
| 2.3 Update — macOS Apple Silicon | | BLOCKED (siehe Hinweis) |
| 2.4 Update — Linux | | |
| 2.5 Update — fehlendes Asset | | |
| 3.A Safe Pull — lokale Änderung | | |
| 3.B Safe Pull — Änderung + ungetrackte Datei | | |
| 4.1 index.lock — verwaist | | |
| 4.2 index.lock — aktiver Prozess | | |
| 4.3 index.lock — MERGE_HEAD | | |
| 5.1–5.9 Regression | | |

**Bewertungsregel:** Für ein **GO** müssen alle Zeilen außer den ausdrücklich als
BLOCKED markierten macOS-Update-Tests (2.2/2.3) mit GO bewertet sein. Ein einziges
FAIL in Block 4 (insbesondere 4.2) führt automatisch zu **NO GO**, unabhängig vom
Ergebnis der übrigen Blöcke, da hier reales Datenverlustrisiko besteht.

```text
Release Candidate
GO / NO GO   (Zutreffendes eintragen)
```

**Begründung:**

_______________________________________________________________

_______________________________________________________________

**Falls NO GO:** Konkrete fehlgeschlagene Tests, betroffene Commits/Dateien und
nächste Schritte hier auflisten, bevor ein erneuter Anlauf gestartet wird.
