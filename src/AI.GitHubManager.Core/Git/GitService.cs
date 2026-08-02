using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Git;

public sealed class GitService
{
    private readonly CommandRunner _runner;
    private readonly RepositoryLockService _lockService;
    private readonly GitLockGuard _lockGuard;

    public GitService(CommandRunner runner) : this(runner, RepositoryLockService.Shared, new GitLockGuard(runner)) { }

    /// <summary>Test/DI seam: inject a fake <see cref="RepositoryLockService"/> or
    /// <see cref="GitLockGuard"/> (e.g. with a fake <see cref="IGitProcessDetector"/>).</summary>
    public GitService(CommandRunner runner, RepositoryLockService lockService, GitLockGuard lockGuard)
    {
        _runner = runner;
        _lockService = lockService;
        _lockGuard = lockGuard;
    }

    /// <summary>Filesystem-only, side-effect-free check of the current lock/interrupted
    /// state — safe to call from UI refresh logic (e.g. "Umgebung prüfen").</summary>
    public GitLockCheckResult CheckLockStatus(string repositoryPath) => _lockGuard.CheckIndexLock(repositoryPath);

    /// <summary>Explicit, user-triggered removal of a verified-orphaned <c>index.lock</c>
    /// (the "Verwaiste Git-Sperre sicher entfernen" button). Serializes against any other
    /// writing operation for this repository just like the other write methods.</summary>
    public async Task<GitLockCheckResult> RemoveOrphanedGitLockAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        using var _ = await _lockService.AcquireAsync(repositoryPath, cancellationToken);
        return await _lockGuard.RemoveOrphanedLockAsync(repositoryPath, cancellationToken);
    }

    public Task<CommandResult> VersionAsync() => _runner.RunAsync("git", ["--version"]);

    public async Task<GitStatusResult> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new GitStatusResult(false, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), "Lokaler Ordner existiert nicht.");

        var inside = await _runner.RunAsync("git", ["rev-parse", "--is-inside-work-tree"], repositoryPath, cancellationToken);
        if (!inside.Success)
            return new GitStatusResult(false, string.Empty, string.Empty, inside.CombinedOutput, Array.Empty<string>(), "Der Ordner ist kein Git-Repository.");

        var branch = await _runner.RunAsync("git", ["branch", "--show-current"], repositoryPath, cancellationToken);
        var remote = await _runner.RunAsync("git", ["remote", "get-url", "origin"], repositoryPath, cancellationToken);
        var status = await _runner.RunAsync("git", ["status", "--porcelain"], repositoryPath, cancellationToken);

        if (!branch.Success)
            return new GitStatusResult(false, string.Empty, string.Empty, branch.CombinedOutput, Array.Empty<string>(), "Aktiver Branch konnte nicht gelesen werden.");

        if (!status.Success)
            return new GitStatusResult(false, branch.StandardOutput.Trim(), string.Empty, status.CombinedOutput, Array.Empty<string>(), "Git-Status konnte nicht gelesen werden.");

        var files = status.StandardOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToArray();

        var remoteOrigin = remote.Success ? remote.StandardOutput.Trim() : string.Empty;
        var error = remote.Success ? null : "Remote 'origin' ist nicht gesetzt.";

        return new GitStatusResult(true, branch.StandardOutput.Trim(), remoteOrigin, status.StandardOutput, files, error);
    }

    public Task<CommandResult> PullAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => PullAsync(repositoryPath, null, null, cancellationToken);

    public async Task<CommandResult> PullAsync(
        string repositoryPath,
        string? defaultBranch = null,
        string? remoteUrl = null,
        CancellationToken cancellationToken = default)
    {
        using var _ = await _lockService.AcquireAsync(repositoryPath, cancellationToken);
        var lockGuardResult = await _lockGuard.EnsureWritableAsync(repositoryPath, cancellationToken);
        if (lockGuardResult is not null) return lockGuardResult;

        var repositoryCheck = await EnsureRepositoryAsync(repositoryPath, remoteUrl, cancellationToken);
        if (repositoryCheck is not null) return repositoryCheck;

        var upstream = await EnsureUpstreamAsync(repositoryPath, defaultBranch, cancellationToken);
        if (!upstream.Success && !upstream.IsFallbackAvailable)
            return upstream.Result;

        var branch = await GetCurrentBranchOrDefaultAsync(repositoryPath, defaultBranch, cancellationToken);
        var result = !upstream.Success && !string.IsNullOrWhiteSpace(branch)
            ? await _runner.RunAsync("git", ["pull", "--ff-only", "origin", branch], repositoryPath, cancellationToken)
            : await _runner.RunAsync("git", ["pull", "--ff-only"], repositoryPath, cancellationToken);

        if (!result.Success)
        {
            if (CanRecoverFromUntrackedOverwrite(result) && !string.IsNullOrWhiteSpace(branch))
                return await RecoverUntrackedOverwriteAndPullAsync(repositoryPath, branch, result, cancellationToken);

            // Branches have diverged — fast-forward not possible → rebase
            bool isDiverged = result.CombinedOutput.Contains("Not possible to fast-forward", StringComparison.OrdinalIgnoreCase)
                           || result.CombinedOutput.Contains("diverged", StringComparison.OrdinalIgnoreCase);
            if (isDiverged)
            {
                result = await _runner.RunAsync("git", ["pull", "--rebase"], repositoryPath, cancellationToken);
                var prefix = result.Success
                    ? "ℹ Branches waren divergiert — Rebase verwendet.\n\n"
                    : "ℹ Rebase-Versuch nach divergierten Branches fehlgeschlagen.\n\n";
                result = new CommandResult(result.ExitCode, prefix + result.StandardOutput, result.StandardError, result.FileName, result.Arguments);
            }
            else
            {
                var (recovered, fixMsg) = await GitErrorRecovery.TryRecoverAsync(repositoryPath, result.CombinedOutput);
                if (recovered)
                {
                    result = await _runner.RunAsync("git", ["pull", "--ff-only"], repositoryPath, cancellationToken);
                    var prefix = fixMsg + "\n\n";
                    result = new CommandResult(result.ExitCode, prefix + result.StandardOutput, result.StandardError, result.FileName, result.Arguments);
                }
            }
        }
        return result;
    }

    public async Task<CommandResult> CommitAndPushAsync(string repositoryPath, string message, CancellationToken cancellationToken = default)
    {
        using var _ = await _lockService.AcquireAsync(repositoryPath, cancellationToken);
        var lockGuardResult = await _lockGuard.EnsureWritableAsync(repositoryPath, cancellationToken);
        if (lockGuardResult is not null) return lockGuardResult;

        var repositoryCheck = await EnsureRepositoryAsync(repositoryPath, cancellationToken);
        if (repositoryCheck is not null) return repositoryCheck;

        // ── Pre-staging safety check ──────────────────────────────────────────
        // Scan untracked files BEFORE git add -A. If any look like scratch/temp
        // files that the user forgot to add to .gitignore, abort with a clear
        // message instead of silently committing garbage.
        var preStatus = await _runner.RunAsync("git", ["status", "--porcelain"], repositoryPath, cancellationToken);
        if (preStatus.Success && !string.IsNullOrWhiteSpace(preStatus.StandardOutput))
        {
            var lines = preStatus.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var suspicious = SuspiciousFileChecker.FindSuspicious(lines);
            if (suspicious.Count > 0)
            {
                var fileList = string.Join("\n  • ", suspicious);
                return new CommandResult(
                    -1,
                    $"⚠ Commit abgebrochen – verdächtige Dateien gefunden:\n\n  • {fileList}\n\n" +
                    $"Diese Dateien gehören wahrscheinlich nicht ins Repository.\n" +
                    $"Bitte .gitignore aktualisieren oder die Dateien löschen,\n" +
                    $"dann erneut committen.",
                    string.Empty, "git", "pre-staging-check");
            }
        }
        // ─────────────────────────────────────────────────────────────────────

        var add = await _runner.RunAsync("git", ["add", "-A"], repositoryPath, cancellationToken);
        if (!add.Success)
        {
            var (recovered, fixMsg) = await GitErrorRecovery.TryRecoverAsync(repositoryPath, add.CombinedOutput);
            if (recovered)
            {
                add = await _runner.RunAsync("git", ["add", "-A"], repositoryPath, cancellationToken);
                if (!add.Success)
                    return new CommandResult(add.ExitCode, fixMsg + "\n\n" + add.StandardOutput, add.StandardError, add.FileName, add.Arguments);
            }
            else return add;
        }

        var porcelain = await _runner.RunAsync("git", ["status", "--porcelain"], repositoryPath, cancellationToken);
        if (!porcelain.Success) return porcelain;

        bool hasChanges = !string.IsNullOrWhiteSpace(porcelain.StandardOutput);

        var safeMessage = message.Trim();
        if (string.IsNullOrWhiteSpace(safeMessage)) safeMessage = $"Update {DateTime.Now:yyyy-MM-dd HH:mm}";

        string commitOutput;
        if (hasChanges)
        {
            var commit = await _runner.RunAsync("git", ["commit", "-m", safeMessage], repositoryPath, cancellationToken);
            if (!commit.Success)
            {
                var (recovered, fixMsg) = await GitErrorRecovery.TryRecoverAsync(repositoryPath, commit.CombinedOutput);
                if (recovered)
                {
                    commit = await _runner.RunAsync("git", ["commit", "-m", safeMessage], repositoryPath, cancellationToken);
                    if (!commit.Success)
                        return new CommandResult(commit.ExitCode, fixMsg + "\n\n" + commit.StandardOutput, commit.StandardError, commit.FileName, commit.Arguments);
                    commitOutput = fixMsg + "\n\n" + commit.CombinedOutput.Trim();
                }
                else return commit;
            }
            else commitOutput = commit.CombinedOutput.Trim();
        }
        else
        {
            var ahead = await HasUnpushedCommitsAsync(repositoryPath, cancellationToken);
            if (!ahead)
            {
                return new CommandResult(0, "Keine Änderungen und keine ungesendeten Commits. Commit und Push übersprungen.", string.Empty, "git", "commit+push");
            }

            commitOutput = "Keine Änderungen. Commit übersprungen, ungesendete Commits werden gepusht.";
        }

        var push = await _runner.RunAsync("git", ["push"], repositoryPath, cancellationToken);
        var combined = commitOutput + "\n\n" + push.CombinedOutput.Trim();
        return new CommandResult(push.ExitCode, combined, string.Empty, "git", "commit+push");
    }

    public Task<CommandResult> SetRemoteOriginAsync(string repositoryPath, string remoteUrl, CancellationToken cancellationToken = default)
        => _runner.RunAsync("git", ["remote", "set-url", "origin", remoteUrl], repositoryPath, cancellationToken);

    // ── Advanced/dangerous operations (Teil B2) ─────────────────────────────
    // Every method here is only ever reached after the UI has shown an
    // explicit, un-defaulted, non-Enter-triggered confirmation dialog
    // (Teil B7/D) — none of these run automatically or silently. All ref
    // arguments are validated with GitRefValidator before being placed in
    // the argument list (Teil D: no unsafe input, no shell concatenation —
    // arguments are always passed as a discrete array to the process, never
    // built as a command string).

    /// <summary>Force push using --force-with-lease (never plain --force) — Teil B2.
    /// The only advanced operation that genuinely needs a remote, so it uses the
    /// full <see cref="EnsureRepositoryAsync(string,CancellationToken)"/> check
    /// (origin must exist) rather than the local-only check the others use.</summary>
    public async Task<CommandResult> ForcePushWithLeaseAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        using var _ = await _lockService.AcquireAsync(repositoryPath, cancellationToken);
        var lockGuardResult = await _lockGuard.EnsureWritableAsync(repositoryPath, cancellationToken);
        if (lockGuardResult is not null) return lockGuardResult;

        var repositoryCheck = await EnsureRepositoryAsync(repositoryPath, cancellationToken);
        if (repositoryCheck is not null) return repositoryCheck;

        return await _runner.RunAsync("git", ["push", "--force-with-lease"], repositoryPath, cancellationToken);
    }

    /// <summary>Irreversibly deletes untracked files/folders (git clean -fd) — Teil B2.
    /// Purely local — no remote required.</summary>
    public Task<CommandResult> CleanAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => RunGuardedLocalWriteAsync(repositoryPath, ["clean", "-fd"], cancellationToken);

    /// <summary>Non-hard reset — keeps changes as unstaged (git reset &lt;ref&gt;) — Teil B2.</summary>
    public Task<CommandResult> ResetAsync(string repositoryPath, string targetRef, CancellationToken cancellationToken = default)
        => RunGuardedLocalWriteWithRefAsync(repositoryPath, "reset", targetRef, cancellationToken);

    /// <summary>Irreversibly discards uncommitted changes (git reset --hard &lt;ref&gt;) — Teil B2.</summary>
    public Task<CommandResult> HardResetAsync(string repositoryPath, string targetRef, CancellationToken cancellationToken = default)
        => RunGuardedLocalWriteWithRefAsync(repositoryPath, "reset --hard", targetRef, cancellationToken, ["reset", "--hard"]);

    /// <summary>Replays local commits onto a new base, rewriting history (git rebase &lt;ref&gt;) — Teil B2.</summary>
    public Task<CommandResult> RebaseAsync(string repositoryPath, string targetRef, CancellationToken cancellationToken = default)
        => RunGuardedLocalWriteWithRefAsync(repositoryPath, "rebase", targetRef, cancellationToken);

    /// <summary>Applies a single commit onto the current branch (git cherry-pick &lt;ref&gt;) — Teil B2.</summary>
    public Task<CommandResult> CherryPickAsync(string repositoryPath, string commitRef, CancellationToken cancellationToken = default)
        => RunGuardedLocalWriteWithRefAsync(repositoryPath, "cherry-pick", commitRef, cancellationToken);

    private Task<CommandResult> RunGuardedLocalWriteWithRefAsync(
        string repositoryPath, string commandLabel, string targetRef, CancellationToken cancellationToken, string[]? argPrefix = null)
    {
        var error = GitRefValidator.ValidationError(targetRef);
        if (error is not null)
            return Task.FromResult(new CommandResult(-1, string.Empty, error.Value.De, "git", commandLabel));

        var args = (argPrefix ?? [commandLabel]).Append(targetRef.Trim()).ToArray();
        return RunGuardedLocalWriteAsync(repositoryPath, args, cancellationToken);
    }

    /// <summary>Lock-guarded write for operations that only touch the local
    /// repository — checks that the path is a real git work tree, but does
    /// NOT require a configured "origin" remote (unlike
    /// <see cref="EnsureRepositoryAsync(string,CancellationToken)"/>, which
    /// backs Pull/Commit+Push/ForcePush and is remote-oriented).</summary>
    private async Task<CommandResult> RunGuardedLocalWriteAsync(string repositoryPath, string[] args, CancellationToken cancellationToken)
    {
        using var _ = await _lockService.AcquireAsync(repositoryPath, cancellationToken);
        var lockGuardResult = await _lockGuard.EnsureWritableAsync(repositoryPath, cancellationToken);
        if (lockGuardResult is not null) return lockGuardResult;

        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new CommandResult(-1, string.Empty, "Lokaler Ordner existiert nicht.", "git", string.Empty);

        var inside = await _runner.RunAsync("git", ["rev-parse", "--is-inside-work-tree"], repositoryPath, cancellationToken);
        if (!inside.Success)
            return new CommandResult(inside.ExitCode, inside.StandardOutput, "Der Ordner ist kein Git-Repository.\n" + inside.StandardError, "git", "rev-parse --is-inside-work-tree");

        return await _runner.RunAsync("git", args, repositoryPath, cancellationToken);
    }

    private async Task<CommandResult?> EnsureRepositoryAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
        => await EnsureRepositoryAsync(repositoryPath, null, cancellationToken);

    private async Task<CommandResult?> EnsureRepositoryAsync(
        string repositoryPath,
        string? remoteUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new CommandResult(-1, string.Empty, "Lokaler Ordner existiert nicht.", "git", string.Empty);

        var inside = await _runner.RunAsync("git", ["rev-parse", "--is-inside-work-tree"], repositoryPath, cancellationToken);
        if (!inside.Success)
            return new CommandResult(inside.ExitCode, inside.StandardOutput, "Der Ordner ist kein Git-Repository.\n" + inside.StandardError, "git", "rev-parse --is-inside-work-tree");

        var remote = await _runner.RunAsync("git", ["remote", "get-url", "origin"], repositoryPath, cancellationToken);
        if (!remote.Success)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl))
                return new CommandResult(remote.ExitCode, remote.StandardOutput, "Remote 'origin' ist nicht gesetzt.\n" + remote.StandardError, "git", "remote get-url origin");

            var add = await _runner.RunAsync("git", ["remote", "add", "origin", remoteUrl.Trim()], repositoryPath, cancellationToken);
            if (!add.Success)
                return add;
        }

        return null;
    }

    private async Task<(bool Success, bool IsFallbackAvailable, CommandResult Result)> EnsureUpstreamAsync(
        string repositoryPath,
        string? defaultBranch,
        CancellationToken cancellationToken)
    {
        var existing = await _runner.RunAsync(
            "git",
            ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"],
            repositoryPath,
            cancellationToken);
        if (existing.Success)
            return (true, false, existing);

        var branch = await GetCurrentBranchOrDefaultAsync(repositoryPath, defaultBranch, cancellationToken);
        if (string.IsNullOrWhiteSpace(branch))
            return (false, false, existing);

        var fetch = await _runner.RunAsync("git", ["fetch", "origin"], repositoryPath, cancellationToken);
        if (!fetch.Success)
            return (false, false, fetch);

        var upstream = await _runner.RunAsync(
            "git",
            ["branch", "--set-upstream-to=origin/" + branch, branch],
            repositoryPath,
            cancellationToken);

        return upstream.Success ? (true, false, upstream) : (false, true, upstream);
    }

    private async Task<string> GetCurrentBranchOrDefaultAsync(
        string repositoryPath,
        string? defaultBranch,
        CancellationToken cancellationToken)
    {
        var branch = await _runner.RunAsync("git", ["branch", "--show-current"], repositoryPath, cancellationToken);
        var current = branch.StandardOutput.Trim();
        return string.IsNullOrWhiteSpace(current) ? defaultBranch?.Trim() ?? string.Empty : current;
    }

    private static bool CanRecoverFromUntrackedOverwrite(CommandResult result)
        => !result.Success &&
           result.CombinedOutput.Contains("untracked working tree files would be overwritten", StringComparison.OrdinalIgnoreCase);

    private async Task<CommandResult> RecoverUntrackedOverwriteAndPullAsync(
        string repositoryPath,
        string branch,
        CommandResult originalPull,
        CancellationToken cancellationToken)
    {
        var head = await _runner.RunAsync("git", ["rev-parse", "--verify", "HEAD"], repositoryPath, cancellationToken);
        if (head.Success)
            return originalPull;

        var reset = await _runner.RunAsync("git", ["reset", "--mixed", "origin/" + branch], repositoryPath, cancellationToken);
        if (!reset.Success)
            return CombineRecoveryResult(originalPull, reset, "Auto-Recovery fehlgeschlagen.");

        var upstream = await _runner.RunAsync(
            "git",
            ["branch", "--set-upstream-to=origin/" + branch, branch],
            repositoryPath,
            cancellationToken);

        var pull = await _runner.RunAsync("git", ["pull", "--ff-only"], repositoryPath, cancellationToken);

        var output =
            "Auto-Recovery ausgeführt: Lokaler Branch hatte noch keinen Commit, " +
            $"deshalb wurde der Index nicht-destruktiv mit origin/{branch} verbunden.\n" +
            "Lokale Dateiinhalte wurden beibehalten und erscheinen jetzt als normale Änderungen.\n\n" +
            originalPull.CombinedOutput.Trim() +
            "\n\n--- Recovery ---\n" +
            reset.CombinedOutput.Trim() +
            "\n" +
            upstream.CombinedOutput.Trim() +
            "\n\n--- Pull danach ---\n" +
            pull.CombinedOutput.Trim();

        return new CommandResult(pull.ExitCode, output, string.Empty, "git", "auto-recover-pull");
    }

    private static CommandResult CombineRecoveryResult(
        CommandResult original,
        CommandResult recovery,
        string message)
    {
        var output = message + "\n\n--- Ursprünglicher Pull ---\n" +
                     original.CombinedOutput.Trim() +
                     "\n\n--- Recovery ---\n" +
                     recovery.CombinedOutput.Trim();

        return new CommandResult(recovery.ExitCode, output, string.Empty, "git", "auto-recover-pull");
    }

    private async Task<bool> HasUnpushedCommitsAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var upstream = await _runner.RunAsync("git", ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], repositoryPath, cancellationToken);
        if (!upstream.Success) return true;

        var count = await _runner.RunAsync("git", ["rev-list", "--count", "@{u}..HEAD"], repositoryPath, cancellationToken);
        return count.Success && int.TryParse(count.StandardOutput.Trim(), out var commits) && commits > 0;
    }
}
