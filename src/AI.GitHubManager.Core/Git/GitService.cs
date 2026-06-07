using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Git;

public sealed class GitService
{
    private readonly CommandRunner _runner;

    public GitService(CommandRunner runner) => _runner = runner;

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

    public async Task<CommandResult> PullAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var repositoryCheck = await EnsureRepositoryAsync(repositoryPath, cancellationToken);
        if (repositoryCheck is not null) return repositoryCheck;

        var result = await _runner.RunAsync("git", ["pull", "--ff-only"], repositoryPath, cancellationToken);
        if (!result.Success)
        {
            var (recovered, fixMsg) = await GitErrorRecovery.TryRecoverAsync(repositoryPath, result.CombinedOutput);
            if (recovered)
            {
                result = await _runner.RunAsync("git", ["pull", "--ff-only"], repositoryPath, cancellationToken);
                var prefix = fixMsg + "\n\n";
                result = new CommandResult(result.ExitCode, prefix + result.StandardOutput, result.StandardError, result.FileName, result.Arguments);
            }
        }
        return result;
    }

    public async Task<CommandResult> CommitAndPushAsync(string repositoryPath, string message, CancellationToken cancellationToken = default)
    {
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

    private async Task<CommandResult?> EnsureRepositoryAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new CommandResult(-1, string.Empty, "Lokaler Ordner existiert nicht.", "git", string.Empty);

        var inside = await _runner.RunAsync("git", ["rev-parse", "--is-inside-work-tree"], repositoryPath, cancellationToken);
        if (!inside.Success)
            return new CommandResult(inside.ExitCode, inside.StandardOutput, "Der Ordner ist kein Git-Repository.\n" + inside.StandardError, "git", "rev-parse --is-inside-work-tree");

        var remote = await _runner.RunAsync("git", ["remote", "get-url", "origin"], repositoryPath, cancellationToken);
        if (!remote.Success)
            return new CommandResult(remote.ExitCode, remote.StandardOutput, "Remote 'origin' ist nicht gesetzt.\n" + remote.StandardError, "git", "remote get-url origin");

        return null;
    }

    private async Task<bool> HasUnpushedCommitsAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var upstream = await _runner.RunAsync("git", ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], repositoryPath, cancellationToken);
        if (!upstream.Success) return true;

        var count = await _runner.RunAsync("git", ["rev-list", "--count", "@{u}..HEAD"], repositoryPath, cancellationToken);
        return count.Success && int.TryParse(count.StandardOutput.Trim(), out var commits) && commits > 0;
    }
}
