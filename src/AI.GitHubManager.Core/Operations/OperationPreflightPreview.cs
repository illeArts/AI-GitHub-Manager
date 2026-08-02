namespace AI.GitHubManager.Core.Operations;

/// <summary>
/// Builds the "Was passiert jetzt?" step list and "Zusammenfassung" block shown
/// directly under the operation dropdown (Teil B6), for the operations that are
/// actually executable in this milestone. Every line here is derived from the
/// <see cref="GitOperationDefinition"/>'s own already-reviewed, truthful fields
/// (WhatChanges/WhatStays/TechnicalCommand) — nothing here is fabricated or
/// speculative live data (live, run-specific facts such as ahead/behind counts
/// or remote reachability are reported by the actual preflight/result services
/// at execution time instead, and are intentionally not duplicated here).
/// </summary>
public static class OperationPreflightPreview
{
    /// <summary>
    /// Returns an ordered list of plain-language steps describing what will
    /// happen if the given operation is run right now, or an empty list if no
    /// canned preview exists yet for this operation id.
    /// </summary>
    public static IReadOnlyList<string> Steps(GitOperationDefinition operation, bool english)
    {
        return operation.Id switch
        {
            "update" => english
                ? new[]
                {
                    "1. Check whether the remote repository has new commits (git fetch).",
                    "2. If local changes exist, back them up safely first (stash).",
                    "3. Merge the new remote commits into the local branch (git pull), if possible as a fast-forward.",
                    "4. If local changes were backed up, restore them afterwards.",
                    "5. Report the exact outcome — success, conflict, or a specific reason it could not proceed.",
                }
                : new[]
                {
                    "1. Prüfen, ob im Online-Repository neue Commits vorliegen (git fetch).",
                    "2. Falls lokale Änderungen vorhanden sind, diese zuerst sicher zwischenspeichern (Stash).",
                    "3. Die neuen Online-Commits in den lokalen Branch übernehmen (git pull), wenn möglich per Fast-Forward.",
                    "4. Falls lokale Änderungen zwischengespeichert wurden, diese danach wiederherstellen.",
                    "5. Das genaue Ergebnis melden — Erfolg, Konflikt, oder ein konkreter Grund, warum es nicht möglich war.",
                },

            "status" => english
                ? new[]
                {
                    "1. Read the current branch, remote, and working-tree state (git status) — read-only.",
                    "2. List changed, staged, and untracked files, if any.",
                    "3. Report the result. Nothing on disk or in the repository is changed.",
                }
                : new[]
                {
                    "1. Aktuellen Branch, Remote und Arbeitsverzeichnis-Zustand lesen (git status) — rein lesend.",
                    "2. Geänderte, vorgemerkte und unversionierte Dateien auflisten, falls vorhanden.",
                    "3. Das Ergebnis melden. Es wird nichts auf der Festplatte oder im Repository verändert.",
                },

            "commit-and-upload" => english
                ? new[]
                {
                    "1. Record the currently staged/changed files as a new commit with the given message.",
                    "2. Upload (push) the new commit to the configured remote branch.",
                    "3. Report the result, including whether the upload was rejected (e.g. remote has newer commits).",
                }
                : new[]
                {
                    "1. Die aktuell vorgemerkten/geänderten Dateien als neuen Commit mit der angegebenen Nachricht festhalten.",
                    "2. Den neuen Commit zum konfigurierten Remote-Branch hochladen (push).",
                    "3. Das Ergebnis melden, einschließlich einer Ablehnung des Uploads (z. B. weil online neuere Commits liegen).",
                },

            _ => Array.Empty<string>(),
        };
    }

    /// <summary>
    /// A one-paragraph "Zusammenfassung" for the given operation, or null if
    /// no canned summary exists yet. Always references the operation's own
    /// WhatChanges/WhatStays text rather than restating something new.
    /// </summary>
    public static string? Summary(GitOperationDefinition operation, bool english)
    {
        if (operation.Id is not ("update" or "status" or "commit-and-upload"))
            return null;

        return english
            ? $"Summary: {operation.WhatChanges(english: true)} {operation.WhatStays(english: true)}"
            : $"Zusammenfassung: {operation.WhatChanges(english: false)} {operation.WhatStays(english: false)}";
    }
}
