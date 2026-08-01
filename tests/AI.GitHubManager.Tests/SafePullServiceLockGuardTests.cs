using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Verifies that <see cref="SafePullService"/> respects the index.lock / repository-lock
/// guard described in "AI GitHub Manager" v1.6.2: an active-looking lock blocks the whole
/// stash/pull/restore sequence before anything is touched, while a verified-orphaned lock
/// is removed automatically and the safe pull proceeds exactly as before.
/// </summary>
public sealed class SafePullServiceLockGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-safepull-lockguard-tests", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public SafePullServiceLockGuardTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task PullAsync_ActiveLockDetected_BlocksBeforeAnyStashOrPull()
    {
        var (repo, _) = await CreateClonedRepoAsync("active-lock-blocks-pull");

        // A local change that WOULD normally be stashed by a safe pull.
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local edit that must not be touched");
        WriteBackdatedFile(Path.Combine(repo, ".git", "index.lock"), TimeSpan.FromSeconds(30));

        var lockGuard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = true });
        var service = new SafePullService(_runner, new RepositoryLockService(), lockGuard);

        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.WriteBlockedByLockGuard, result.State);
        Assert.False(result.Success);

        // Nothing was stashed — the local change is still sitting in the working tree untouched.
        Assert.Equal("local edit that must not be touched", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
        var stashList = await Git(repo, ["stash", "list"]);
        Assert.Equal(string.Empty, stashList.StandardOutput.Trim());

        // The lock file itself was never deleted while a process might be active.
        Assert.True(File.Exists(Path.Combine(repo, ".git", "index.lock")));
    }

    [Fact]
    public async Task PullAsync_ForeignStashPresent_ActiveLockBlocks_ForeignStashUntouched()
    {
        var (repo, _) = await CreateClonedRepoAsync("foreign-stash-untouched");

        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "content for foreign stash");
        await Git(repo, ["stash", "push", "--include-untracked", "--message", "someone else's stash"]);

        WriteBackdatedFile(Path.Combine(repo, ".git", "index.lock"), TimeSpan.FromSeconds(30));

        var lockGuard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = true });
        var service = new SafePullService(_runner, new RepositoryLockService(), lockGuard);

        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.WriteBlockedByLockGuard, result.State);

        var stashList = await Git(repo, ["stash", "list"]);
        Assert.Single(stashList.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("someone else's stash", stashList.StandardOutput);
    }

    [Fact]
    public async Task PullAsync_OrphanedLock_IsRemovedAutomatically_AndSafePullStillWorksAfterward()
    {
        var (repo, remoteSeed) = await CreateClonedRepoAsync("orphaned-lock-then-pull-works");

        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local edit to restore");
        WriteBackdatedFile(Path.Combine(repo, ".git", "index.lock"), TimeSpan.FromSeconds(30));

        await File.WriteAllTextAsync(Path.Combine(remoteSeed, "OTHER.md"), "from remote");
        await Git(remoteSeed, ["add", "OTHER.md"]);
        await Git(remoteSeed, ["commit", "-m", "Remote change"]);
        await Git(remoteSeed, ["push"]);

        var lockGuard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = false });
        var service = new SafePullService(_runner, new RepositoryLockService(), lockGuard);

        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        // The orphaned lock was removed automatically, and the safe pull then ran exactly
        // as it would have without ever having had a lock file at all.
        Assert.Equal(SafePullState.PullSucceededAndChangesRestored, result.State);
        Assert.True(result.Success);
        Assert.False(File.Exists(Path.Combine(repo, ".git", "index.lock")));
        Assert.Equal("local edit to restore", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
        Assert.True(File.Exists(Path.Combine(repo, "OTHER.md")));
    }

    [Fact]
    public async Task PullAsync_NoLockAtAll_BehavesExactlyAsBefore()
    {
        var (repo, _) = await CreateClonedRepoAsync("no-lock-regression-check");

        var lockGuard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = false });
        var service = new SafePullService(_runner, new RepositoryLockService(), lockGuard);

        var result = await service.PullAsync(repo, warnOnlyOnLocalChanges: false);

        Assert.Equal(SafePullState.CleanPullSucceeded, result.State);
        Assert.True(result.Success);
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

    private static void WriteBackdatedFile(string path, TimeSpan age)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
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
