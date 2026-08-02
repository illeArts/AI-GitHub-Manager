using System.Linq;

namespace AI.GitHubManager.Core.Operations;

/// <summary>
/// The fixed catalog of Git operations shown to the user (Teil B1/B2).
///
/// Design decision — "Änderungen prüfen" vs. "Änderungen herunterladen":
/// the brief lists both as separate normal operations, but flags that they
/// may be identical and asks to either separate them clearly or merge them
/// if keeping both would be misleading. Both map to the exact same
/// technical action (<c>git fetch</c>, read-only, zero working-tree impact)
/// with no way to make their outcome meaningfully different for a
/// non-expert user. Presenting two menu entries for one action would be the
/// duplicate/misleading UI the brief explicitly warns against, so they are
/// merged into the single operation <see cref="CheckForChanges"/>, whose
/// title and description say plainly that this is both "prüfen" and
/// "herunterladen (Vorschau)" in one step. This is why the normal list
/// below has 12 entries instead of the 13 named in the brief.
/// </summary>
public static class GitOperationCatalog
{
    /// <summary>Id of the operation selected by default (Teil B1/C: "Aktualisieren").</summary>
    public const string DefaultOperationId = "update";

    public static readonly GitOperationDefinition CheckForChanges = new()
    {
        Id = "check-for-changes",
        TitleDe = "Änderungen prüfen",
        TitleEn = "Check for changes",
        TechnicalCommand = "git fetch",
        DescriptionDe =
            "Lädt neue Informationen vom Online-Repository, ohne deine Arbeitsdateien zu verändern. " +
            "Entspricht technisch auch \"Änderungen herunterladen\" — beides ist derselbe Vorgang " +
            "(git fetch) und wird deshalb hier zusammengefasst.",
        DescriptionEn =
            "Downloads new information from the online repository without changing your working files. " +
            "Technically the same as \"download changes\" — both are the same underlying action " +
            "(git fetch), so they are combined into one operation here.",
        SuitableForDe = "Nachsehen, ob online etwas Neues vorhanden ist, ohne etwas zu übernehmen.",
        SuitableForEn = "Checking whether anything new exists online, without applying it.",
        WhatChangesDe = "Nur interne Informationen über den Remote-Stand (z. B. \"3 Commits voraus\").",
        WhatChangesEn = "Only internal information about the remote state (e.g. \"3 commits ahead\").",
        WhatStaysDe = "Alle Arbeitsdateien bleiben unverändert.",
        WhatStaysEn = "All working files remain unchanged.",
        RiskDe = "Kein Risiko — es werden keine Dateien verändert.",
        RiskEn = "No risk — no files are changed.",
        RiskLevel = GitOperationRiskLevel.Safe,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Update = new()
    {
        Id = DefaultOperationId,
        TitleDe = "Aktualisieren",
        TitleEn = "Update",
        TechnicalCommand = "git pull",
        DescriptionDe =
            "Lädt neue Änderungen aus dem Online-Repository und verbindet sie mit deinem lokalen Stand.",
        DescriptionEn =
            "Downloads new changes from the online repository and integrates them into your local state.",
        SuitableForDe = "Den normalen täglichen Abgleich.",
        SuitableForEn = "The normal daily sync.",
        WhatChangesDe = "Dateien des aktuellen Branches können auf den neuesten Stand gebracht werden.",
        WhatChangesEn = "Files on the current branch can be brought up to date.",
        WhatStaysDe = "Lokale Änderungen bleiben erhalten, solange keine Konflikte mit denselben Dateien entstehen.",
        WhatStaysEn = "Local changes are preserved as long as no conflicts arise in the same files.",
        RiskDe = "Wenn dieselben Stellen lokal und online bearbeitet wurden, kann ein Konflikt entstehen.",
        RiskEn = "If the same spots were edited both locally and online, a conflict can occur.",
        RiskLevel = GitOperationRiskLevel.Caution,
        IsExecutable = true,
    };

    public static readonly GitOperationDefinition UploadChanges = new()
    {
        Id = "upload-changes",
        TitleDe = "Änderungen hochladen",
        TitleEn = "Upload changes",
        TechnicalCommand = "git push",
        DescriptionDe = "Lädt deine bereits erstellten, lokalen Commits zum Online-Repository hoch.",
        DescriptionEn = "Uploads your already-created local commits to the online repository.",
        SuitableForDe = "Fertige, lokal committete Arbeit sichern, ohne vorher weitere Änderungen zu committen.",
        SuitableForEn = "Backing up finished, already-committed work without committing anything new first.",
        WhatChangesDe = "Der Online-Stand des aktuellen Branches wird um deine lokalen Commits ergänzt.",
        WhatChangesEn = "The online state of the current branch gains your local commits.",
        WhatStaysDe = "Deine lokalen Dateien bleiben unverändert.",
        WhatStaysEn = "Your local files remain unchanged.",
        RiskDe = "Wird abgelehnt, wenn online neuere Commits existieren, die du noch nicht hast (kein Datenverlust).",
        RiskEn = "Rejected if newer commits exist online that you don't have yet (no data loss).",
        RiskLevel = GitOperationRiskLevel.Caution,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Commit = new()
    {
        Id = "commit",
        TitleDe = "Commit erstellen",
        TitleEn = "Create commit",
        TechnicalCommand = "git commit",
        DescriptionDe = "Hält deine aktuellen Änderungen lokal als neuen Punkt in der Historie fest.",
        DescriptionEn = "Records your current changes locally as a new point in the history.",
        SuitableForDe = "Arbeit lokal festhalten, aber noch nicht auf GitHub hochladen.",
        SuitableForEn = "Recording work locally without uploading it to GitHub yet.",
        WhatChangesDe = "Ein neuer Commit wird lokal erstellt.",
        WhatChangesEn = "A new commit is created locally.",
        WhatStaysDe = "Der Online-Stand bleibt unverändert, solange nicht zusätzlich hochgeladen wird.",
        WhatStaysEn = "The online state stays unchanged unless you also upload afterwards.",
        RiskDe = "Gering — ein Commit lässt sich rückgängig machen, solange er nicht hochgeladen wurde.",
        RiskEn = "Low — a commit can be undone as long as it hasn't been uploaded.",
        RiskLevel = GitOperationRiskLevel.Safe,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition CommitAndUpload = new()
    {
        Id = "commit-and-upload",
        TitleDe = "Commit erstellen und hochladen",
        TitleEn = "Create commit and upload",
        TechnicalCommand = "git commit && git push",
        DescriptionDe = "Committet deine aktuellen Änderungen und lädt sie danach zum Online-Repository hoch.",
        DescriptionEn = "Commits your current changes and then uploads them to the online repository.",
        SuitableForDe = "Deine Arbeit vollständig auf GitHub sichern.",
        SuitableForEn = "Fully backing up your work on GitHub.",
        WhatChangesDe = "Ein neuer Commit wird erstellt und lokal sowie online sichtbar.",
        WhatChangesEn = "A new commit is created and becomes visible both locally and online.",
        WhatStaysDe = "Andere Dateien und Branches bleiben unverändert.",
        WhatStaysEn = "Other files and branches remain unchanged.",
        RiskDe = "Wird der Push abgelehnt, bleibt der Commit lokal erhalten — es geht nichts verloren.",
        RiskEn = "If the push is rejected, the commit stays local — nothing is lost.",
        RiskLevel = GitOperationRiskLevel.Caution,
        IsExecutable = true,
    };

    public static readonly GitOperationDefinition Status = new()
    {
        Id = "status",
        TitleDe = "Repository-Status anzeigen",
        TitleEn = "Show repository status",
        TechnicalCommand = "git status",
        DescriptionDe = "Zeigt Branch, Remote-URL und alle geänderten Dateien an.",
        DescriptionEn = "Shows branch, remote URL, and all changed files.",
        SuitableForDe = "Einen Überblick über den aktuellen Zustand bekommen.",
        SuitableForEn = "Getting an overview of the current state.",
        WhatChangesDe = "Nichts — reine Anzeige.",
        WhatChangesEn = "Nothing — display only.",
        WhatStaysDe = "Alles bleibt unverändert.",
        WhatStaysEn = "Everything remains unchanged.",
        RiskDe = "Kein Risiko.",
        RiskEn = "No risk.",
        RiskLevel = GitOperationRiskLevel.Safe,
        IsExecutable = true,
    };

    public static readonly GitOperationDefinition CreateBranch = new()
    {
        Id = "create-branch",
        TitleDe = "Branch erstellen",
        TitleEn = "Create branch",
        TechnicalCommand = "git checkout -b <name>",
        DescriptionDe = "Legt einen neuen, parallelen Arbeitsbereich innerhalb desselben Repositorys an.",
        DescriptionEn = "Creates a new, parallel line of work within the same repository.",
        SuitableForDe = "Einen neuen Arbeitsbereich beginnen, ohne den aktuellen Branch zu verändern.",
        SuitableForEn = "Starting new work without changing the current branch.",
        WhatChangesDe = "Ein neuer Branch wird angelegt und ausgecheckt.",
        WhatChangesEn = "A new branch is created and checked out.",
        WhatStaysDe = "Bestehende Branches bleiben unverändert.",
        WhatStaysEn = "Existing branches remain unchanged.",
        RiskDe = "Gering — ein ungenutzter Branch lässt sich jederzeit löschen.",
        RiskEn = "Low — an unused branch can be deleted at any time.",
        RiskLevel = GitOperationRiskLevel.Safe,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition SwitchBranch = new()
    {
        Id = "switch-branch",
        TitleDe = "Branch wechseln",
        TitleEn = "Switch branch",
        TechnicalCommand = "git checkout <name>",
        DescriptionDe = "Wechselt zu einem vorhandenen Arbeitsbereich innerhalb desselben Repositorys.",
        DescriptionEn = "Switches to an existing line of work within the same repository.",
        SuitableForDe = "Zu vorhandener Arbeit wechseln.",
        SuitableForEn = "Switching to existing work.",
        WhatChangesDe = "Die Arbeitsdateien werden auf den Stand des Ziel-Branches gebracht.",
        WhatChangesEn = "Working files are brought to the state of the target branch.",
        WhatStaysDe = "Committete Arbeit auf dem bisherigen Branch bleibt erhalten.",
        WhatStaysEn = "Committed work on the previous branch is preserved.",
        RiskDe = "Nicht committete lokale Änderungen können den Wechsel verhindern oder mitgenommen werden.",
        RiskEn = "Uncommitted local changes can block the switch or come along with it.",
        RiskLevel = GitOperationRiskLevel.Caution,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Stash = new()
    {
        Id = "stash",
        TitleDe = "Änderungen zwischenspeichern",
        TitleEn = "Stash changes",
        TechnicalCommand = "git stash push",
        DescriptionDe = "Legt deine aktuellen, nicht committeten Änderungen sicher beiseite.",
        DescriptionEn = "Safely sets your current, uncommitted changes aside.",
        SuitableForDe = "Vorübergehend an etwas anderem arbeiten, ohne aktuelle Änderungen zu verlieren.",
        SuitableForEn = "Temporarily working on something else without losing current changes.",
        WhatChangesDe = "Die Arbeitsdateien werden auf den letzten Commit zurückgesetzt; die Änderungen bleiben gespeichert.",
        WhatChangesEn = "Working files are reset to the last commit; the changes remain saved.",
        WhatStaysDe = "Die Änderungen selbst gehen nicht verloren, nur der sichtbare Zustand ändert sich.",
        WhatStaysEn = "The changes themselves aren't lost, only the visible state changes.",
        RiskDe = "Gering — die Sicherung lässt sich jederzeit wiederherstellen.",
        RiskEn = "Low — the backup can be restored at any time.",
        RiskLevel = GitOperationRiskLevel.Safe,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition StashRestore = new()
    {
        Id = "stash-restore",
        TitleDe = "Zwischengespeicherte Änderungen wiederherstellen",
        TitleEn = "Restore stashed changes",
        TechnicalCommand = "git stash apply",
        DescriptionDe = "Holt zuvor zwischengespeicherte Änderungen zurück in die Arbeitsdateien.",
        DescriptionEn = "Brings previously stashed changes back into the working files.",
        SuitableForDe = "Zwischengespeicherte Arbeit fortsetzen.",
        SuitableForEn = "Continuing previously stashed work.",
        WhatChangesDe = "Die Arbeitsdateien erhalten die zwischengespeicherten Änderungen zurück.",
        WhatChangesEn = "Working files get the stashed changes back.",
        WhatStaysDe = "Die Sicherung bleibt zusätzlich erhalten (apply, nicht pop).",
        WhatStaysEn = "The backup is additionally kept (apply, not pop).",
        RiskDe = "Kann Konflikte mit zwischenzeitlichen Änderungen erzeugen.",
        RiskEn = "Can create conflicts with changes made in the meantime.",
        RiskLevel = GitOperationRiskLevel.Caution,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Merge = new()
    {
        Id = "merge",
        TitleDe = "Merge durchführen",
        TitleEn = "Perform merge",
        TechnicalCommand = "git merge <branch>",
        DescriptionDe = "Führt die Historie eines anderen Branches mit dem aktuellen Branch zusammen.",
        DescriptionEn = "Combines the history of another branch with the current branch.",
        SuitableForDe = "Abgeschlossene Arbeit aus einem anderen Branch übernehmen.",
        SuitableForEn = "Bringing finished work from another branch into this one.",
        WhatChangesDe = "Der aktuelle Branch erhält die Commits des zusammengeführten Branches.",
        WhatChangesEn = "The current branch gains the commits of the merged branch.",
        WhatStaysDe = "Der andere Branch bleibt unverändert bestehen.",
        WhatStaysEn = "The other branch remains unchanged.",
        RiskDe = "Kann Konflikte erzeugen, die manuell aufgelöst werden müssen.",
        RiskEn = "Can create conflicts that need to be resolved manually.",
        RiskLevel = GitOperationRiskLevel.Caution,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Diff = new()
    {
        Id = "diff",
        TitleDe = "Änderungen vergleichen",
        TitleEn = "Compare changes",
        TechnicalCommand = "git diff",
        DescriptionDe = "Zeigt eine verständliche Zusammenfassung plus die technische Diff-Ansicht der Änderungen.",
        DescriptionEn = "Shows a plain-language summary plus the technical diff view of the changes.",
        SuitableForDe = "Versehentliche oder unklare Änderungen untersuchen.",
        SuitableForEn = "Investigating accidental or unclear changes.",
        WhatChangesDe = "Nichts — reine Anzeige.",
        WhatChangesEn = "Nothing — display only.",
        WhatStaysDe = "Alles bleibt unverändert.",
        WhatStaysEn = "Everything remains unchanged.",
        RiskDe = "Kein Risiko.",
        RiskEn = "No risk.",
        RiskLevel = GitOperationRiskLevel.Safe,
        IsExecutable = false,
    };

    // ── Advanced operations (Teil B2) — never in the normal list, never
    // auto-selected, always require extra confirmation. ─────────────────────

    public static readonly GitOperationDefinition Rebase = new()
    {
        Id = "rebase",
        TitleDe = "Rebase",
        TitleEn = "Rebase",
        TechnicalCommand = "git rebase <branch>",
        DescriptionDe = "Setzt deine Commits auf einen neuen Basispunkt um und schreibt dabei die Historie neu.",
        DescriptionEn = "Replays your commits onto a new base, rewriting history in the process.",
        SuitableForDe = "Eine saubere, lineare Historie erzeugen — erfordert Git-Erfahrung.",
        SuitableForEn = "Producing a clean, linear history — requires Git experience.",
        WhatChangesDe = "Commit-Hashes und die Reihenfolge der Historie ändern sich.",
        WhatChangesEn = "Commit hashes and the order of history change.",
        WhatStaysDe = "Der Inhalt deiner Änderungen bleibt inhaltlich erhalten.",
        WhatStaysEn = "The content of your changes is preserved.",
        RiskDe = "Bereits hochgeladene Commits werden dabei ungültig — kann Folgeprobleme für andere verursachen.",
        RiskEn = "Already-uploaded commits become invalid — can cause follow-on problems for others.",
        RiskLevel = GitOperationRiskLevel.Advanced,
        IsAdvanced = true,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition CherryPick = new()
    {
        Id = "cherry-pick",
        TitleDe = "Cherry-Pick",
        TitleEn = "Cherry-pick",
        TechnicalCommand = "git cherry-pick <commit>",
        DescriptionDe = "Übernimmt einen einzelnen Commit aus einem anderen Branch in den aktuellen Branch.",
        DescriptionEn = "Applies a single commit from another branch onto the current branch.",
        SuitableForDe = "Gezielt einen einzelnen Commit übernehmen, ohne den ganzen Branch zu mergen.",
        SuitableForEn = "Selectively taking a single commit without merging the whole branch.",
        WhatChangesDe = "Ein neuer Commit mit demselben Inhalt entsteht auf dem aktuellen Branch.",
        WhatChangesEn = "A new commit with the same content is created on the current branch.",
        WhatStaysDe = "Der Ursprungs-Branch bleibt unverändert.",
        WhatStaysEn = "The source branch remains unchanged.",
        RiskDe = "Kann Konflikte erzeugen; derselbe Inhalt kann versehentlich doppelt übernommen werden.",
        RiskEn = "Can create conflicts; the same content can accidentally be applied twice.",
        RiskLevel = GitOperationRiskLevel.Advanced,
        IsAdvanced = true,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition ForcePush = new()
    {
        Id = "force-push",
        TitleDe = "Force Push",
        TitleEn = "Force push",
        TechnicalCommand = "git push --force-with-lease",
        DescriptionDe = "Überschreibt die Online-Historie mit deiner lokalen Historie.",
        DescriptionEn = "Overwrites the online history with your local history.",
        SuitableForDe = "Nur nach bewusster History-Umschreibung (z. B. nach einem Rebase) auf einem eigenen Branch.",
        SuitableForEn = "Only after a deliberate history rewrite (e.g. after a rebase) on your own branch.",
        WhatChangesDe = "Der Online-Branch wird auf deinen lokalen Stand zurückgesetzt.",
        WhatChangesEn = "The online branch is reset to your local state.",
        WhatStaysDe = "Nichts wird geschützt — bereits von anderen abgerufene Commits können verwaist zurückbleiben.",
        WhatStaysEn = "Nothing is protected — commits others already fetched can be left orphaned.",
        RiskDe = "Kann fremde Arbeit auf dem Remote überschreiben und unwiederbringlich verlieren.",
        RiskEn = "Can overwrite and irrecoverably lose other people's work on the remote.",
        RiskLevel = GitOperationRiskLevel.Dangerous,
        IsAdvanced = true,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Reset = new()
    {
        Id = "reset",
        TitleDe = "Reset (ohne Hard-Modus)",
        TitleEn = "Reset (non-hard)",
        TechnicalCommand = "git reset <commit>",
        DescriptionDe = "Setzt den aktuellen Branch auf einen früheren Commit zurück, Änderungen bleiben als unstaged erhalten.",
        DescriptionEn = "Resets the current branch to an earlier commit; changes are kept unstaged.",
        SuitableForDe = "Commits rückgängig machen, ohne die Änderungen selbst zu verlieren.",
        SuitableForEn = "Undoing commits without losing the changes themselves.",
        WhatChangesDe = "Der Branch-Zeiger bewegt sich zurück; Staging-Status ändert sich.",
        WhatChangesEn = "The branch pointer moves back; staging status changes.",
        WhatStaysDe = "Der Dateiinhalt bleibt als unstaged Änderung im Arbeitsverzeichnis erhalten.",
        WhatStaysEn = "File content is preserved as unstaged changes in the working directory.",
        RiskDe = "Bereits hochgeladene Commits werden lokal ungültig, falls sie zurückgesetzt werden.",
        RiskEn = "Already-uploaded commits become locally invalid if they are reset away.",
        RiskLevel = GitOperationRiskLevel.Advanced,
        IsAdvanced = true,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition HardReset = new()
    {
        Id = "hard-reset",
        TitleDe = "Hard Reset",
        TitleEn = "Hard reset",
        TechnicalCommand = "git reset --hard <commit>",
        DescriptionDe = "Setzt den Branch zurück und verwirft dabei alle nicht committeten Änderungen unwiderruflich.",
        DescriptionEn = "Resets the branch and irreversibly discards all uncommitted changes.",
        SuitableForDe = "Nur wenn du bewusst und vollständig auf einen früheren Stand zurück willst.",
        SuitableForEn = "Only when you deliberately and completely want to go back to an earlier state.",
        WhatChangesDe = "Arbeitsdateien werden ohne Rückfrage auf den Zielzustand überschrieben.",
        WhatChangesEn = "Working files are overwritten to the target state without asking again.",
        WhatStaysDe = "Nichts — nicht committete Änderungen gehen verloren.",
        WhatStaysEn = "Nothing — uncommitted changes are lost.",
        RiskDe = "Unwiderruflicher Verlust nicht committeter lokaler Änderungen.",
        RiskEn = "Irreversible loss of uncommitted local changes.",
        RiskLevel = GitOperationRiskLevel.Dangerous,
        IsAdvanced = true,
        IsExecutable = false,
    };

    public static readonly GitOperationDefinition Clean = new()
    {
        Id = "clean",
        TitleDe = "Clean",
        TitleEn = "Clean",
        TechnicalCommand = "git clean -fd",
        DescriptionDe = "Löscht alle nicht von Git verfolgten Dateien und Ordner unwiderruflich.",
        DescriptionEn = "Irreversibly deletes all files and folders not tracked by Git.",
        SuitableForDe = "Ein komplett sauberes Arbeitsverzeichnis erzwingen (z. B. vor einem CI-Test).",
        SuitableForEn = "Forcing a completely clean working directory (e.g. before a CI test).",
        WhatChangesDe = "Alle untracked Dateien und Ordner werden gelöscht.",
        WhatChangesEn = "All untracked files and folders are deleted.",
        WhatStaysDe = "Von Git verfolgte, committete Dateien bleiben unangetastet.",
        WhatStaysEn = "Git-tracked, committed files remain untouched.",
        RiskDe = "Unwiderruflicher Verlust aller nicht committeten neuen Dateien.",
        RiskEn = "Irreversible loss of all uncommitted new files.",
        RiskLevel = GitOperationRiskLevel.Dangerous,
        IsAdvanced = true,
        IsExecutable = false,
    };

    /// <summary>Normal operations, in menu order. 12 entries (see class doc for why not 13).</summary>
    public static readonly IReadOnlyList<GitOperationDefinition> NormalOperations = new[]
    {
        CheckForChanges, Update, UploadChanges, Commit, CommitAndUpload,
        Status, CreateBranch, SwitchBranch, Stash, StashRestore, Merge, Diff,
    };

    /// <summary>Advanced operations (Teil B2) — shown only in a separate, collapsed area.</summary>
    public static readonly IReadOnlyList<GitOperationDefinition> AdvancedOperations = new[]
    {
        Rebase, CherryPick, ForcePush, Reset, HardReset, Clean,
    };

    public static readonly IReadOnlyList<GitOperationDefinition> All =
        NormalOperations.Concat(AdvancedOperations).ToArray();

    /// <summary>Looks up an operation by id, or null if unknown.</summary>
    public static GitOperationDefinition? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(o => o.Id == id);

    /// <summary>
    /// Whether an operation's selection is safe to persist as "last used"
    /// (Teil B7/C: dangerous operations must never come back as an automatic
    /// default on next launch).
    /// </summary>
    public static bool IsSafeToPersistAsDefault(GitOperationDefinition operation) =>
        operation.RiskLevel is GitOperationRiskLevel.Safe or GitOperationRiskLevel.Caution;
}
