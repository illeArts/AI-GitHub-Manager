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
        CmdPull.Text   = L.T("Fast-Forward Pull — sicher, bricht bei Konflikten ab.",
                             "Fast-forward pull — safe, aborts on conflicts.");
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

        // Troubleshooting
        TsHeader.Text = L.T("Fehlerbehebung", "Troubleshooting");
        Ts1.Text = L.T("\"gh not found\" / \"gh nicht gefunden\"  →  \"GitHub CLI installieren\" klicken, App neu starten.",
                       "\"gh not found\"  →  Click \"GitHub CLI installieren\", restart app.");
        Ts2.Text = L.T("\"Authentication failed\"  →  \"GitHub Login\" klicken.",
                       "\"Authentication failed\"  →  Click \"GitHub Login\".");
        Ts3.Text = L.T("\"refusing to allow ... workflow\"  →  \"GitHub Rechte: repo + workflow\" klicken.",
                       "\"refusing to allow ... workflow\"  →  Click \"GitHub Rechte: repo + workflow\".");
        Ts4.Text = L.T("\"non-fast-forward\"  →  Zuerst \"Pull\" klicken, dann erneut \"Commit + Push\".",
                       "\"non-fast-forward\"  →  Click \"Pull\" first, then retry \"Commit + Push\".");
        Ts5.Text = L.T("\"Keine Änderungen – Commit übersprungen\"  →  Keine Änderungen erkannt, Push wurde trotzdem ausgeführt.",
                       "\"Keine Änderungen – Commit übersprungen\"  →  No changes detected, push was still executed.");

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
