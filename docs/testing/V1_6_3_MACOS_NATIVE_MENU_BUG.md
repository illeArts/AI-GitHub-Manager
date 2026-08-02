# macOS: Einstellungen/Hilfe/Über real nicht erreichbar (v1.6.2, behoben in 1.6.3)

## Korrektur einer früheren Einschätzung

In der Diskussion zu diesem Fund wurde zunächst vermutet, der Screenshot des
Nutzers zeige eine veraltete, vor der Einführung von Settings/Help/About
gebaute Installation. **Das war falsch.** Der Screenshot zeigte die frisch
aus dem damaligen `main`-Stand (v1.6.2, Commit `cf7c9bf`) gebaute und
gestartete macOS-App. Einstellungen, Hilfe und Über waren im Quellcode
vorhanden, aber auf macOS real nicht wie vorgesehen erreichbar. Es handelte
sich um einen echten Plattform-/Menü-Fehler, nicht um einen veralteten Build.

## Root Cause

`App.axaml.cs::SetupMacNativeMenu` rief `NativeMenu.SetMenu(Current!, menu)`
mit einem selbst zusammengesetzten Menü auf, dessen erstes Element
(`settingsItem`) ein **Blatt-Element ohne Untermenü** war:

```csharp
var settingsItem = new NativeMenuItem(L.T("Einstellungen", "Settings"));
...
var menu = new NativeMenu();
menu.Add(settingsItem);   // erstes Top-Level-Element — kein Submenü!
menu.Add(helpMenu);
NativeMenu.SetMenu(Current!, menu);
```

Auf macOS wird das **erste Top-Level-Element** einer `NativeMenu` als
Anwendungsmenü behandelt und von macOS automatisch mit dem echten
App-Namen beschriftet — es wird aber ein Untermenü (`NativeMenu`-Kind)
erwartet, das typischerweise mindestens „Über …" und „Beenden" enthält.
Weil hier stattdessen ein einzelnes Blatt-Element ohne Kinder als erstes
Element übergeben wurde, hat `NativeMenu.SetMenu` das von Avalonia/macOS
sonst automatisch bereitgestellte Standard-App-Menü (mit „Über AI GitHub
Manager" und „AI GitHub Manager beenden") **stillschweigend überschrieben
und nicht durch ein gültiges Äquivalent ersetzt**. Zusätzlich:

- Es gab keine Tastaturkürzel (`Gesture`) für Einstellungen (⌘,) oder
  Hilfe (⌘?) — beide vom Nutzer erwarteten macOS-Konventionen fehlten.
- „Über AI GitHub Manager" war zwei Ebenen tief im Hilfe-Untermenü
  versteckt, statt im Anwendungsmenü zu stehen, wo macOS-Nutzer es
  erwarten.
- Es gab keinen expliziten „Beenden"-Eintrag mit ⌘Q im selbst gebauten
  Menü.

Das erklärt, warum der Nutzer auf einer real gebauten und gestarteten
macOS-App weder ein erkennbares Anwendungsmenü noch Einstellungen/Hilfe/Über
an der erwarteten Stelle vorfand, obwohl der Code dafür existierte.

## Fix (v1.6.3, Commit `60e42a9`)

`SetupMacNativeMenu` baut jetzt ein vollständiges, macOS-konformes Menü:

- **Erstes Top-Level-Element** ist ein `NativeMenuItem` mit eigenem
  `NativeMenu` (Submenü) — das ist die Voraussetzung dafür, dass macOS es
  korrekt als Anwendungsmenü behandelt und umbenennt. Enthält: „Über AI
  GitHub Manager", Trenner, „Einstellungen…" (⌘,), Trenner, „AI GitHub
  Manager beenden" (⌘Q, ruft `IClassicDesktopStyleApplicationLifetime.
  Shutdown()` auf, nicht `Environment.Exit`).
- Zweites Top-Level-Element: „Hilfe" mit „Hilfe / Befehle" (⌘?).
- Beschriftungen bleiben weiterhin über `L.Changed` sprachsynchron;
  Tastaturkürzel sind sprachunabhängig und ändern sich nicht.

## Status

Der Fix ist committet und gebaut (0 Fehler, 0 Warnungen, 159/159 Tests
weiterhin grün). **Nicht in dieser Sitzung real auf einem Mac getestet** —
das erfordert einen Blick auf die tatsächliche macOS-Menüleiste
(`open`/Doppelklick, dann ⌘, / ⌘? / Anwendungsmenü prüfen). Bitte bei der
nächsten macOS-Testrunde (Meilenstein 5) gezielt verifizieren.
