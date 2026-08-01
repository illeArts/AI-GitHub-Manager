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
    public string ActionsHeader     => L.T("Aktionen",              "Actions");
    public string AddProject        => L.T("+ Hinzufügen",          "+ Add");
    public string RemoveProject     => L.T("− Entfernen",           "− Remove");
    public string ImportGitHub      => L.T("Von GitHub importieren","Import from GitHub");
    public string InstallCli        => L.T("GitHub CLI installieren","Install GitHub CLI");
    public string GitHubLogin       => L.T("GitHub Login",          "GitHub Login");
    public string CheckEnv          => L.T("Umgebung prüfen",       "Check Environment");
    public string RefreshScope      => L.T("GitHub Rechte: repo + workflow",
                                          "GitHub Scopes: repo + workflow");
    public string RepairGit         => L.T("Git Credentials reparieren","Repair Git Credentials");
    public string RepairEnvironmentToken => L.T("⚠ Ungültigen Token entfernen und Anmeldung reparieren",
                                              "⚠ Remove invalid token and repair login");
    public string SanitizeRemote    => L.T("⚠ Remote sicher bereinigen", "⚠ Clean up remote safely");
    public string ExportDiagnostics => L.T("Diagnosebericht exportieren", "Export diagnostic report");
    public string ExportProjectBtn  => L.T("Projekt exportieren",    "Export project");
    public string DownloadUpdateBtn => L.T("⬇ Jetzt herunterladen",  "⬇ Download now");
    public string SaveProject       => L.T("Projekt speichern",     "Save Project");
    public string CheckForUpdate    => L.T("Auf Updates prüfen",    "Check for Updates");
    public string BuildTestPushBtn  => L.T("🔧 Build, Test & Push", "🔧 Build, Test & Push");
    public string CreateInstallerBtn => L.T("📦 Installer erstellen", "📦 Create Installer");

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

    // ── About window ──────────────────────────────────────────────────────────
    public string AboutTitle       => L.T("Über", "About");
    public string AboutDescription => L.T(
        "AI GitHub Manager — sicheres, einfaches GitHub-Management für alle deine Projekte. Push, Pull, Diagnose und GitHub CLI-Setup auf einen Klick. Läuft auf Windows und macOS.",
        "AI GitHub Manager — secure, simple GitHub management for all your projects. Push, Pull, diagnostics, and GitHub CLI setup at the click of a button. Runs on Windows and macOS.");

    // ── Export window (Clean Export) ──────────────────────────────────────────
    public string ExportWindowTitle    => L.T("Projekt exportieren · Clean Export", "Export project · Clean Export");
    public string ExportHeaderTitle    => L.T("Projekt exportieren", "Export project");
    public string ExportHeaderSubtitle => L.T("Clean Export · Export und Zusatzarchiv, keine Git-Synchronisierung",
                                             "Clean Export · Export and extra archive, no Git synchronization");
    public string ExportSourceLabel    => L.T("Quelle", "Source");
    public string ExportPickSourceBtn  => L.T("Ordner wählen", "Browse...");
    public string ExportDestinationLabel => L.T("Zielarchiv", "Destination archive");
    public string ExportPickDestinationBtn => L.T("Ziel wählen", "Choose destination");
    public string ExportProfileLabel   => L.T("Profil", "Profile");
    public string ExportCustomPatternsLabel => L.T("Eigene Ausschlüsse (ein Muster pro Zeile)",
                                                   "Custom exclusions (one pattern per line)");
    public string ExportPreviewLabel   => L.T("Vorschau", "Preview");
    public string ExportSensitiveLabel => L.T("Möglicherweise sensible Dateien (markiert = ausschließen)",
                                             "Potentially sensitive files (checked = exclude)");
    public string ExportSensitiveConfirmation => L.T(
        "Ich bestätige bewusst, dass nicht ausgeschlossene sensible Dateien in das ZIP aufgenommen werden.",
        "I intentionally confirm that sensitive files not excluded will be included in the ZIP.");
    public string ExportRefreshPreviewBtn => L.T("Vorschau aktualisieren", "Refresh preview");
    public string ExportCancelBtn      => L.T("Abbrechen", "Cancel");
    public string ExportCreateZipBtn   => L.T("ZIP erstellen", "Create ZIP");
    public string ExportOpenFolderBtn  => L.T("Zielordner öffnen", "Open destination folder");
    public string ExportStatusReady    => L.T("Bereit.", "Ready.");

    // Runtime status messages from ExportWindow.axaml.cs (bilingual pairs, looked up via L.T at the call site)
    public string ExportStatusScanning        => L.T("Ordner wird gescannt …", "Scanning folder …");
    public string ExportStatusPreviewReady    => L.T("Vorschau bereit.", "Preview ready.");
    public string ExportStatusConfirmSensitive => L.T(
        "Bitte sensible Dateien markieren oder deren Aufnahme ausdrücklich bestätigen.",
        "Please mark sensitive files or explicitly confirm their inclusion.");
    public string ExportStatusCancelled       => L.T("Export abgebrochen; unvollständiges Archiv wurde entfernt.",
                                                     "Export cancelled; incomplete archive was removed.");
    public string ExportStatusInputsChanged   => L.T("Eingaben geändert – Vorschau aktualisieren.",
                                                     "Inputs changed – refresh the preview.");
    public string ExportValidationFailedPrefix => L.T("Validierung fehlgeschlagen: ", "Validation failed: ");
    public string ExportFailedPrefix          => L.T("Export fehlgeschlagen: ", "Export failed: ");
}
