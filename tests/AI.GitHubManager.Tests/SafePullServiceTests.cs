using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

public sealed class SafePullServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-safepull-tests", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public SafePullServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task PullAsync_CleanWorktree_PullsSuccessfully()
    {
        var (repo, _) = await CreateClonedRepoAsync("clean-repo");

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.CleanPullSucceeded, result.State);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task PullAsync_TrackedLocalChange_BackupPullRestoreSucceeds()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("tracked-change-repo");

        // Local, uncommitted change to a tracked file.
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local edit");

        // A new commit lands on the remote in the meantime.
        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "OTHER.md"), "from remote");
        await Git(remoteSeed, ["add", "OTHER.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote change"]);
        await Git(remoteSeed, ["push"]);

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.PullSucceededAndChangesRestored, result.State);
        Assert.True(result.Success);
        Assert.Equal("local edit", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
        Assert.True(File.Exists(Path.Combine(repo, "OTHER.md")));

        // Backup stash must have been dropped after a verified-clean restore.
        var stashList = await Git(repo, ["stash", "list"]);
        Assert.Equal(string.Empty, stashList.StandardOutput.Trim());
    }

    [Fact]
    public async Task PullAsync_UntrackedFile_IsFullyPreserved()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("untracked-file-repo");

        await File.WriteAllTextAsync(Path.Combine(repo, "scratch.txt"), "untracked content");

        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "OTHER.md"), "from remote");
        await Git(remoteSeed, ["add", "OTHER.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote change"]);
        await Git(remoteSeed, ["push"]);

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.PullSucceededAndChangesRestored, result.State);
        Assert.True(File.Exists(Path.Combine(repo, "scratch.txt")));
        Assert.Equal("untracked content", await File.ReadAllTextAsync(Path.Combine(repo, "scratch.txt")));
    }

    [Fact]
    public async Task PullAsync_MultipleLocalChanges_AllFullyRestored()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("multi-change-repo");

        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "edit 1");
        await File.WriteAllTextAsync(Path.Combine(repo, "scratch-a.txt"), "new file a");
        await File.WriteAllTextAsync(Path.Combine(repo, "scratch-b.txt"), "new file b");

        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "OTHER.md"), "from remote");
        await Git(remoteSeed, ["add", "OTHER.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote change"]);
        await Git(remoteSeed, ["push"]);

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.PullSucceededAndChangesRestored, result.State);
        Assert.Equal("edit 1", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
        Assert.Equal("new file a", await File.ReadAllTextAsync(Path.Combine(repo, "scratch-a.txt")));
        Assert.Equal("new file b", await File.ReadAllTextAsync(Path.Combine(repo, "scratch-b.txt")));
        Assert.True(File.Exists(Path.Combine(repo, "OTHER.md")));
    }

    [Fact]
    public async Task PullAsync_ExistingUserStash_IsNeverTouched()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("existing-stash-repo");

        // A pre-existing stash created by the user, unrelated to our run.
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "user stash content");
        await Git(repo, ["stash", "push", "--include-untracked", "--message", "user's own stash"]);

        // New local change for our run to back up.
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "safe pull local edit");

        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "OTHER.md"), "from remote");
        await Git(remoteSeed, ["add", "OTHER.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote change"]);
        await Git(remoteSeed, ["push"]);

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.PullSucceededAndChangesRestored, result.State);

        // Our backup was dropped; the user's own stash must still be present, untouched.
        var stashList = await Git(repo, ["stash", "list"]);
        Assert.Single(stashList.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("user's own stash", stashList.StandardOutput);
    }

    [Fact]
    public async Task PullAsync_PullFails_RestoresOriginalStateAndKeepsFilesIntact()
    {
        var repo = CreatePath("pull-fails-repo");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "initial");
        await Git(repo, ["add", "README.md"]);
        await Git(repo, ["commit", "-m", "Initial"]);

        // No remote/upstream configured at all → "git pull --ff-only" fails deterministically.
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local edit before failing pull");
        await File.WriteAllTextAsync(Path.Combine(repo, "scratch.txt"), "untracked, must survive");

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.PullFailedAndRestored, result.State);
        Assert.False(result.Success);
        Assert.Equal("local edit before failing pull", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
        Assert.True(File.Exists(Path.Combine(repo, "scratch.txt")));

        var stashList = await Git(repo, ["stash", "list"]);
        Assert.Equal(string.Empty, stashList.StandardOutput.Trim());
    }

    [Fact]
    public async Task PullAsync_RestoreConflict_KeepsBackupAndReportsConflictFiles()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("restore-conflict-repo");

        // Local uncommitted change to README.md.
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local conflicting edit");

        // A conflicting change to the SAME line lands on the remote.
        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "README.md"), "remote conflicting edit");
        await Git(remoteSeed, ["add", "README.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote conflicting change"]);
        await Git(remoteSeed, ["push"]);

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.RestoreConflict, result.State);
        Assert.False(result.Success);
        Assert.NotNull(result.BackupStashRef);
        Assert.Contains("README.md", result.ConflictFiles);

        // Backup must NOT have been dropped.
        var stashList = await Git(repo, ["stash", "list"]);
        Assert.NotEqual(string.Empty, stashList.StandardOutput.Trim());

        // No local file was deleted — README.md still exists (with conflict markers).
        Assert.True(File.Exists(Path.Combine(repo, "README.md")));
    }

    [Fact]
    public async Task PullAsync_WarnOnlyMode_AbortsAndCreatesNoBackup()
    {
        var (repo, _) = await CreateClonedRepoAsync("warn-only-repo");

        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local edit");

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: true);

        Assert.Equal(SafePullState.AbortedDueToLocalChanges, result.State);
        Assert.False(result.Success);

        var stashList = await Git(repo, ["stash", "list"]);
        Assert.Equal(string.Empty, stashList.StandardOutput.Trim());

        // No local files touched/deleted.
        Assert.Equal("local edit", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
    }

    [Fact]
    public async Task PullAsync_DivergedBranches_NeverAttemptsAutomaticMerge()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("diverged-repo");

        // Local commit (worktree stays clean after committing).
        await File.WriteAllTextAsync(Path.Combine(repo, "LOCAL_ONLY.md"), "local commit");
        await Git(repo, ["add", "LOCAL_ONLY.md"]);
        await Git(repo, ["commit", "-m", "Local-only commit"]);

        // Divergent remote commit.
        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "REMOTE_ONLY.md"), "remote commit");
        await Git(remoteSeed, ["add", "REMOTE_ONLY.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote-only commit"]);
        await Git(remoteSeed, ["push"]);

        var service = new SafePullService(_runner);
        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.FastForwardNotPossible, result.State);
        Assert.False(result.Success);

        // Still on the local commit only — no merge/rebase commit was created.
        var log = await Git(repo, ["log", "--oneline", "-1"]);
        Assert.Contains("Local-only commit", log.StandardOutput);
    }

    private async Task<(string Repo, string RemoteSeed)> CreateClonedRepoAsync(string name)
    {
        var remote = CreatePath(name + "-remote.git");
        var seed = CreatePath(name + "-seed");
        var repo = CreatePath(name);

        await Git(_root, ["init", "--bare", remote]);
        await Git(_root, ["clone", remote, seed]);
        await ConfigureIdentityAsync(seed);
        await File.WriteAllTextAsync(Path.Combine(seed, "README.md"), "initial");
        await Git(seed, ["add", "README.md"]);
        await Git(seed, ["commit", "-m", "Initial"]);
        await Git(seed, ["branch", "-M", "main"]);
        await Git(seed, ["push", "-u", "origin", "main"]);

        await Git(_root, ["clone", remote, repo]);
        await ConfigureIdentityAsync(repo);
        await Git(repo, ["checkout", "main"]);

        return (repo, seed);
    }

    private string CreatePath(string name) => Path.Combine(_root, name);

    private async Task ConfigureIdentityAsync(string repositoryPath)
    {
        await Git(repositoryPath, ["config", "user.email", "tests@example.invalid"]);
        await Git(repositoryPath, ["config", "user.name", "AI GitHub Manager Tests"]);
    }

    private async Task<CommandResult> Git(string workingDirectory, string[] arguments)
    {
        var result = await _runner.RunAsync("git", arguments, workingDirectory);
        Assert.True(result.Success, result.CombinedOutput);
        return result;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, FileAttributes.Normal);

            Directory.Delete(_root, recursive: true);
        }
    }
}
