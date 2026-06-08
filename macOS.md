## macOS — build-installer-mac.sh (auf dem Mac ausführen)

bash build-installer-mac.sh              # für deine CPU (arm64 oder x64)
bash build-installer-mac.sh --universal  # beide Architekturen

## Erstellt dist/AI_GitHub_Manager_1.2.0_macOS_arm64.dmg — öffnen, App in Applications ziehen, fertig.


***

## Windows — build-installer-win.bat (Doppelklick)

1. Publiziert die App als einzelne .exe (alles eingebettet, kein .NET nötig beim Nutzer)
2. Erstellt dist\AI_GitHub_Manager_Setup_1.2.0_win-x64.exe mit Inno Setup

## Voraussetzung: Inno Setup 6 installieren (kostenlos). Das Skript findet es automatisch.
Der Installer legt Start-Menü-Eintrag und optional einen Desktop-Shortcut an, hat einen funktionierenden Deinstaller und unterstützt Deutsch + Englisch.
