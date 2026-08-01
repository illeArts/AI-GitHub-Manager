# Release-Historie — AI GitHub Manager

Fortlaufendes Protokoll aller Release-QA-Abnahmen. Jede Zeile verweist auf die
zugehörige, unveränderlich im Repository verbleibende QA-Datei unter
`docs/testing/RELEASE_QA_v<Version>.md`. Neue Releases werden anhand von
`docs/testing/RELEASE_QA_TEMPLATE.md` vorbereitet und hier nach Abschluss der
Praxisabnahme ergänzt.

| Version | Datum | QA-Datei | Ergebnis | Bekannte Einschränkungen | Getestet von |
|---|---|---|---|---|---|
| v1.6.2 | _(Praxisabnahme ausstehend)_ | [RELEASE_QA_v1.6.2.md](RELEASE_QA_v1.6.2.md) | Build/Tests/Push technisch erfolgreich (157/157 Tests grün); praktische Geräteabnahme (Blöcke 1–5) noch offen | macOS-Update-Tests (2.2/2.3) BLOCKED — kein CI-Job erzeugt `macos-x64.zip`/`macos-arm64.zip`, nur für v1.6.2 gültig | _ausstehend_ |

**Hinweis zu den drei `xUnit1031`-Analyzer-Warnungen (v1.6.2):** Reine
Analyzer-Hinweise („Test methods should not use blocking task operations"), keine
fehlgeschlagenen Tests. Kein Release-Hindernis; als technischer
Verbesserungspunkt für v1.6.3 vorgemerkt.
