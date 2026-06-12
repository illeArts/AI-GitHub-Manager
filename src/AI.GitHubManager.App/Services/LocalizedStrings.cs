using System.ComponentModel;

namespace AI.GitHubManager.App.Services;

/// <summary>
/// Singleton that exposes every UI string as a property.
/// When L.Language changes it fires PropertyChanged(null) — Avalonia re-evaluates
/// every binding that points at this object, so the whole UI switches instantly.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    public static readonly LocalizedStrings Instance = new();

    private LocalizedStrings()
    {
        // null property name = "all properties changed" — Avalonia honours this
        L.Changed += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Header ────────────────────────────────────────────────────────────────
    public string AppSubtitle => L.T(
        "GitHub-Projekte verwalten, lokale Ordner verbinden, Push/Pull kontrolliert ausführen.",
        "Manage GitHub projects, link local folders, and run Push/Pull with confidence.");

    // ── Left panel ────────────────────────────────────────────────────────────
    public string ProjectsHeader    => L.T("Projekte",              "Projects");
    public string AddProject        => L.T("+ Hinzufügen",          "+ Add");
    public string RemoveProject     => L.T("− Entfernen",           "− Remove");
    public string ImportGitHub      => L.T("Von GitHub importieren","Import from GitHub");
    public string InstallCli        => L.T("GitHub CLI installieren","Install GitHub CLI");
    public string GitHubLogin       => L.T("GitHub Login",          "GitHub Login");
    public string CheckEnv          => L.T("Umgebung prüfen",       "Check Environment");
    public string RefreshScope      => L.T("GitHub Rechte: repo + workflow",
                                          "GitHub Scopes: repo + workflow");
    public string RepairGit         => L.T("Git Credentials reparieren","Repair Git Credentials");
    public string SaveProject       => L.T("Projekt speichern",     "Save Project");
    public string CheckForUpdate    => L.T("Auf Updates prüfen",    "Check for Updates");

    // ── Right panel ───────────────────────────────────────────────────────────
    public string LocalLinkHeader   => L.T("Lokale Verknüpfung",   "Local Link");
    public string PathWatermark     => L.T("Lokaler Projektordner, z.B. /Users/... oder C:\\Users\\...",
                                          "Local project folder, e.g. /Users/... or C:\\Users\\...");
    public string CommitWatermark   => L.T("Commit-Nachricht",      "Commit message");
    public string PickFolder        => L.T("Ordner wählen",         "Browse...");
    public string StatusBtn         => "Status";   // same in both languages
    public string PullBtn           => "Pull";     // same in both languages
    public string CommitPushBtn     => "Commit + Push";  // same

    public string InfoBanner        => L.T(
        "Hinweis: Die App speichert keine GitHub-Tokens. Erst GitHub CLI installieren, dann GitHub Login ausführen. Authentifizierung läuft über GitHub CLI (gh) und den sicheren Credential Store des Systems.",
        "Note: This app never stores GitHub tokens. Install GitHub CLI first, then run GitHub Login. Authentication uses GitHub CLI (gh) and the system's secure credential store.");

    public string OutputHeader      => L.T("Ausgabe / Fehleranalyse","Output / Diagnostics");

    // ── Menu (used in code-behind ApplyMenuStrings) ───────────────────────────
    public string MenuSettingsHeader => L.T("_Einstellungen",       "_Settings");
    public string MenuHelpHeader     => L.T("_Hilfe",               "_Help");
    public string MenuHelpItemHeader => L.T("Hilfe / Befehle",      "Help / Commands");
    public string MenuAboutHeader    => L.T("Über AI GitHub Manager","About AI GitHub Manager");
}
