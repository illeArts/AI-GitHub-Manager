using Avalonia.Controls;
using AI.GitHubManager.App.Services;

namespace AI.GitHubManager.App.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        ApplyStrings();
        L.Changed += ApplyStrings;
        Closed += (_, _) => L.Changed -= ApplyStrings;
    }

    private void ApplyStrings()
    {
        Title = L.T("Hilfe — AI GitHub Manager", "Help — AI GitHub Manager");

        TitleText.Text    = L.T("Hilfe & Befehle", "Help & Commands");
        SubtitleText.Text = L.T("Schnellstart, Befehle und Fehlerbehebung",
                                "Quick start, commands and troubleshooting");

        // Quick start
        QsHeader.Text = L.T("Schnellstart", "Quick Start");
        Qs1.Text = L.T("1. \"GitHub CLI installieren\" klicken — GitHub CLI (gh) wird per winget/brew installiert.",
                       "1. Click \"GitHub CLI installieren\" — installs GitHub CLI (gh) via winget/brew.");
        Qs2.Text = L.T("2. \"GitHub Login\" klicken — ein Terminalfenster öffnet sich, Browser-Code bestätigen.",
                       "2. Click \"GitHub Login\" — a terminal window opens, confirm the browser code.");
        Qs3.Text = L.T("3. \"Umgebung prüfen\" klicken — Git, GitHub CLI und Login sollten alle OK zeigen.",
                       "3. Click \"Umgebung prüfen\" — Git, GitHub CLI and Login should all show OK.");
        Qs4.Text = L.T("4. \"+ Hinzufügen\" klicken und lokalen Projektordner auswählen.",
                       "4. Click \"+ Hinzufügen\" and select your local project folder.");
        Qs5.Text = L.T("5. Status / Pull / Commit + Push nutzen um das Repository zu verwalten.",
                       "5. Use Status / Pull / Commit + Push to manage your repository.");

        // Commands
        CmdHeader.Text = L.T("Befehle", "Commands");
        CmdStatus.Text = L.T("Zeigt Branch, Remote-URL und alle geänderten Dateien.",
                             "Shows branch, remote URL, and all changed files.");
        CmdPull.Text   = L.T("Pull — Fast-Forward wenn möglich, sonst automatisch Rebase.",
                             "Pull — fast-forward when possible, otherwise auto-rebase.");
        CmdCommit.Text = L.T("Staged alle Änderungen, commitet mit deiner Nachricht und pusht zum Remote.",
                             "Stages all changes, commits with your message, and pushes to remote.");
        CmdImportLabel.Text = L.T("Von GitHub importieren", "Import from GitHub");
        CmdImport.Text = L.T("Importiert alle Repositories deines GitHub-Accounts (gh repo list).",
                             "Imports all repositories from your GitHub account (gh repo list).");
        CmdEnvLabel.Text = L.T("Umgebung prüfen", "Check Environment");
        CmdEnv.Text    = L.T("Prüft git-Installation, GitHub CLI und Authentifizierungsstatus.",
                             "Verifies git installation, GitHub CLI, and authentication status.");
        CmdScopeLabel.Text = L.T("GitHub Rechte", "GitHub Scopes");
        CmdScope.Text  = L.T("Erneuert OAuth-Scopes um repo + workflow-Rechte einzuschließen.",
                             "Refreshes OAuth scopes to include repo + workflow permissions.");
        CmdGitLabel.Text = L.T("Git Credentials reparieren", "Repair Git Credentials");
        CmdGit.Text    = L.T("Führt gh auth setup-git aus, um den Credential Store zu reparieren.",
                             "Runs gh auth setup-git to repair the credential store.");
        CmdRepairTokenLabel.Text = L.T("Ungültigen Token entfernen…", "Remove invalid token…");
        CmdRepairToken.Text = L.T(
            "Erscheint nur, wenn ein ungültiger GH_TOKEN/GITHUB_TOKEN eine gültige GitHub-Anmeldung blockiert. " +
            "Entfernt nur die betroffene Umgebungsvariable, lässt die Keyring-Anmeldung unangetastet.",
            "Only appears when an invalid GH_TOKEN/GITHUB_TOKEN is blocking a valid GitHub login. " +
            "Removes only the affected environment variable, leaves the keyring login untouched.");
        CmdSanitizeRemoteLabel.Text = L.T("Remote sicher bereinigen", "Clean up remote safely");
        CmdSanitizeRemote.Text = L.T(
            "Erscheint, wenn die Remote-URL Zugangsdaten oder einen Platzhalter wie DEIN_VORHANDENER_TOKEN enthält. " +
            "Setzt die Remote-URL auf die sichere, credential-freie Form zurück.",
            "Appears when the remote URL contains credentials or a placeholder like DEIN_VORHANDENER_TOKEN. " +
            "Resets the remote URL to the safe, credential-free form.");
        CmdDiagnosticsLabel.Text = L.T("Diagnosebericht exportieren", "Export diagnostic report");
        CmdDiagnostics.Text = L.T(
            "Erstellt einen redigierten Bericht (Version, OS, Git/CLI-Version, Remote, Branch, Auth-Status, Scopes) " +
            "zum Weitergeben an einen Entwickler. Enthält niemals Tokens oder Passwörter.",
            "Creates a redacted report (version, OS, Git/CLI version, remote, branch, auth status, scopes) " +
            "to share with a developer. Never contains tokens or passwords.");
        CmdExportProjectLabel.Text = L.T("Projekt exportieren", "Export project");
        CmdExportProject.Text = L.T(
            "Öffnet den Clean-Export-Dialog: erstellt ein ZIP-Archiv des Projekts ohne .git-Verzeichnis, " +
            "mit Erkennung möglicherweise sensibler Dateien.",
            "Opens the Clean Export dialog: creates a ZIP archive of the project without the .git directory, " +
            "with detection of potentially sensitive files.");
        CmdBuildTestPushLabel.Text = L.T("Build, Test & Push", "Build, Test & Push");
        CmdBuildTestPush.Text = L.T(
            "Für .NET-Projekte (z. B. dieses Repository): führt dotnet build, dann dotnet test aus. " +
            "Nur wenn beides erfolgreich war, wird committet und gepusht. Bei Fehlschlag wird nichts gepusht.",
            "For .NET projects (e.g. this repository): runs dotnet build, then dotnet test. " +
            "Only commits and pushes if both succeed. Nothing is pushed on failure.");
        CmdCreateInstallerLabel.Text = L.T("Installer erstellen", "Create Installer");
        CmdCreateInstaller.Text = L.T(
            "Erstellt einen Windows-Installer (Publish + Inno Setup) oder ruft build-installer-mac.sh auf macOS auf. " +
            "Kann einige Minuten dauern. Windows benötigt dafür Inno Setup 6 (kostenlos, https://jrsoftware.org/isinfo.php) " +
            "— fehlt es, erscheint automatisch der Button „Inno Setup installieren“.",
            "Creates a Windows installer (publish + Inno Setup) or runs build-installer-mac.sh on macOS. " +
            "Can take a few minutes. On Windows this requires Inno Setup 6 (free, https://jrsoftware.org/isinfo.php) " +
            "— if it's missing, the “Install Inno Setup” button appears automatically.");
        CmdInstallInnoSetupLabel.Text = L.T("Inno Setup installieren", "Install Inno Setup");
        CmdInstallInnoSetup.Text = L.T(
            "Nur sichtbar unter Windows, solange Inno Setup 6 nicht gefunden wurde. Öffnet die offizielle Download-Seite " +
            "im Standardbrowser — die App lädt oder installiert nichts selbstständig. Nach der Installation wird bei " +
            "jeder Umgebungsprüfung und nach jedem Installer-Lauf automatisch neu geprüft; der Button verschwindet dann " +
            "von selbst, ganz ohne App-Neustart.",
            "Only visible on Windows, and only while Inno Setup 6 hasn't been found. Opens the official download page " +
            "in the default browser — the app never downloads or installs anything on its own. After installation, " +
            "availability is re-checked automatically on every environment check and after every installer run; the " +
            "button then disappears on its own, no app restart needed.");

        // Troubleshooting
        TsHeader.Text = L.T("Fehlerbehebung", "Troubleshooting");
        Ts1.Text = L.T("\"gh not found\" / \"gh nicht gefunden\"  →  \"GitHub CLI installieren\" klicken, App neu starten.",
                       "\"gh not found\"  →  Click \"GitHub CLI installieren\", restart app.");
        Ts2.Text = L.T("\"Authentication failed\"  →  \"GitHub Login\" klicken.",
                       "\"Authentication failed\"  →  Click \"GitHub Login\".");
        Ts3.Text = L.T("\"refusing to allow ... workflow\"  →  \"GitHub Rechte: repo + workflow\" klicken.",
                       "\"refusing to allow ... workflow\"  →  Click \"GitHub Rechte: repo + workflow\".");
        Ts4.Text = L.T("\"non-fast-forward\" / divergierte Branches  →  Pull erkennt das automatisch und führt Rebase durch.",
                       "\"non-fast-forward\" / diverged branches  →  Pull detects this automatically and rebases.");
        Ts5.Text = L.T("\"Keine Änderungen – Commit übersprungen\"  →  Keine Änderungen erkannt, Push wurde trotzdem ausgeführt.",
                       "\"Keine Änderungen – Commit übersprungen\"  →  No changes detected, push was still executed.");
        Ts6.Text = L.T(
            "Warum kann ein ungültiger Token eine gültige GitHub-Anmeldung blockieren?  →  Windows-Umgebungsvariablen " +
            "(GH_TOKEN/GITHUB_TOKEN) haben Vorrang vor der sicheren Anmeldung im Windows-Schlüsselspeicher. Ist so ein " +
            "Token vorhanden aber ungültig (z. B. abgelaufen), schlägt jede GitHub-Aktion fehl — auch wenn die eigentliche " +
            "Anmeldung im Hintergrund noch gültig ist. Der Manager erkennt das automatisch und bietet \"Ungültigen Token " +
            "entfernen und Anmeldung reparieren\" an; dabei wird nur die fehlerhafte Umgebungsvariable entfernt, die " +
            "eigentliche Anmeldung bleibt unangetastet.",
            "Why can an invalid token block a valid GitHub login?  →  Windows environment variables (GH_TOKEN/GITHUB_TOKEN) " +
            "take priority over the secure login stored in the Windows credential store. If such a variable exists but is " +
            "invalid (e.g. expired), every GitHub action fails — even though the actual login underneath is still valid. " +
            "The manager detects this automatically and offers \"Remove invalid token and repair login\"; only the broken " +
            "environment variable is removed, the actual login is left untouched.");

        // Security checklist
        SecHeader.Text = L.T("Sicherheit & Datenschutz", "Security & Privacy");
        SecLine1.Text  = L.T("Keine GitHub-Tokens oder Passwörter werden gespeichert.",
                             "No GitHub tokens or passwords are ever stored.");
        SecLine2.Text  = L.T("Authentifizierung läuft ausschließlich über die offizielle GitHub CLI (gh).",
                             "Authentication runs exclusively through the official GitHub CLI (gh).");
        SecLine3.Text  = L.T("Zugangsdaten liegen im sicheren Credential Store des Betriebssystems (Keychain / Windows Credential Manager).",
                             "Credentials are stored in the OS secure credential store (Keychain / Windows Credential Manager).");
        SecLine4.Text  = L.T("git push --force wird niemals automatisch ausgeführt. Bei Konflikten bricht die App ab.",
                             "git push --force is never executed automatically. On conflicts the app stops and reports.");
    }
}
