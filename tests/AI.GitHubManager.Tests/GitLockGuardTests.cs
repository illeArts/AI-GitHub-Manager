using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>Test double for <see cref="IGitProcessDetector"/> — lets tests deterministically
/// simulate "an external git process is/isn't active" without depending on real OS process state.</summary>
internal sealed class FakeGitProcessDetector : IGitProcessDetector
{
    public bool ActiveProcessPresent { get; set; }

    public bool IsGitProcessActiveFor(string normalizedRepositoryPath) => ActiveProcessPresent;
}

public sealed class GitLockGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-lockguard-tests", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public GitLockGuardTests() => Directory.CreateDirectory(_root);

    // ── No lock present ──────────────────────────────────────────────────────

    [Fact]
    public void CheckIndexLock_NoLockFile_ReportsNoLockPresent()
    {
        var repo = CreateBareGitDir("no-lock-repo");
        var detector = new FakeGitProcessDetector { ActiveProcessPresent = false };
        var guard = new GitLockGuard(_runner, detector);

        var result = guard.CheckIndexLock(repo);

        Assert.Equal(GitLockStatus.NoLockPresent, result.Status);
        Assert.Null(result.LockFilePath);
    }

    // ── Active lock blocks ────────────────────────────────────────────────────

    [Fact]
    public void CheckIndexLock_LockPresent_ActiveProcessDetected_IsNeverConsideredOrphaned()
    {
        var repo = CreateBareGitDir("active-lock-repo");
        WriteBackdatedLockFile(repo, TimeSpan.FromSeconds(30)); // old enough on its own

        var detector = new FakeGitProcessDetector { ActiveProcessPresent = true };
        var guard = new GitLockGuard(_runner, detector);

        var result = guard.CheckIndexLock(repo);

        Assert.Equal(GitLockStatus.ActiveProcessDetected, result.Status);
    }

    [Fact]
    public void CheckIndexLock_LockTooRecent_TreatedAsPossiblyActiveEvenWithoutDetectedProcess()
    {
        var repo = CreateBareGitDir("fresh-lock-repo");
        WriteBackdatedLockFile(repo, TimeSpan.Zero); // brand new

        var detector = new FakeGitProcessDetector { ActiveProcessPresent = false };
        var guard = new GitLockGuard(_runner, detector);

        var result = guard.CheckIndexLock(repo);

        Assert.Equal(GitLockStatus.ActiveProcessDetected, result.Status);
    }

    // ── Orphaned lock is removed in a controlled way ─────────────────────────

    [Fact]
    public void CheckIndexLock_OldLock_NoActiveProcess_IsOrphanedRemovable()
    {
        var repo = CreateBareGitDir("orphaned-lock-repo");
        WriteBackdatedLockFile(repo, TimeSpan.FromSeconds(30));

        var detector = new FakeGitProcessDetector { ActiveProcessPresent = false };
        var guard = new GitLockGuard(_runner, detector);

        var result = guard.CheckIndexLock(repo);

        Assert.Equal(GitLockStatus.OrphanedRemovable, result.Status);
        Assert.NotNull(result.LockFilePath);
        Assert.True(File.Exists(result.LockFilePath));
    }

    [Fact]
    public async Task RemoveOrphanedLockAsync_OrphanedLock_RemovesFileAndConfirmsWithGitStatus()
    {
        var repo = await InitRealGitRepoAsync("removable-lock-repo");
        var lockPath = Path.Combine(repo, ".git", "index.lock");
        WriteBackdatedFile(lockPath, TimeSpan.FromSeconds(30));

        var detector = new FakeGitProcessDetector { ActiveProcessPresent = false };
        var guard = new GitLockGuard(_runner, detector);

        var result = await guard.RemoveOrphanedLockAsync(repo);

        Assert.Equal(GitLockStatus.RemovedSuccessfully, result.Status);
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public async Task RemoveOrphanedLockAsync_ActiveProcessDetected_NeverDeletesTheFile()
    {
        var repo = await InitRealGitRepoAsync("blocked-removal-repo");
        var lockPath = Path.Combine(repo, ".git", "index.lock");
        WriteBackdatedFile(lockPath, TimeSpan.FromSeconds(30));

        var detector = new FakeGitProcessDetector { ActiveProcessPresent = true };
        var guard = new GitLockGuard(_runner, detector);

        var result = await guard.RemoveOrphanedLockAsync(repo);

        Assert.Equal(GitLockStatus.ActiveProcessDetected, result.Status);
        Assert.True(File.Exists(lockPath), "A lock that might still be owned by an active process must never be deleted.");
    }

    [Fact]
    public async Task RemoveOrphanedLockAsync_ReVerifiesRightBeforeDeleting_RefusesIfNoLongerOrphaned()
    {
        // Simulates the check-then-act race: by the time removal is attempted, an
        // active process is now detected — removal must refuse even though an
        // earlier CheckIndexLock call said it was orphaned.
        var repo = await InitRealGitRepoAsync("race-repo");
        var lockPath = Path.Combine(repo, ".git", "index.lock");
        WriteBackdatedFile(lockPath, TimeSpan.FromSeconds(30));

        var detector = new FakeGitProcessDetector { ActiveProcessPresent = false };
        var guard = new GitLockGuard(_runner, detector);

        var firstCheck = guard.CheckIndexLock(repo);
        Assert.Equal(GitLockStatus.OrphanedRemovable, firstCheck.Status);

        detector.ActiveProcessPresent = true; // race: another process shows up right before removal

        var removal = await guard.RemoveOrphanedLockAsync(repo);

        Assert.Equal(GitLockStatus.ActiveProcessDetected, removal.Status);
        Assert.True(File.Exists(lockPath));
    }

    // ── Interrupted states are reported, never auto-cleaned ──────────────────

    [Theory]
    [InlineData("MERGE_HEAD", GitInterruptedStateKind.Merge)]
    [InlineData("CHERRY_PICK_HEAD", GitInterruptedStateKind.CherryPick)]
    [InlineData("REBASE_HEAD", GitInterruptedStateKind.Rebase)]
    [InlineData("BISECT_LOG", GitInterruptedStateKind.Bisect)]
    public void DetectInterruptedState_KnownMarkerFiles_AreReportedNotCleaned(string markerFile, GitInterruptedStateKind expectedKind)
    {
        var repo = CreateBareGitDir("interrupted-" + markerFile.ToLowerInvariant());
        var markerPath = Path.Combine(repo, ".git", markerFile);
        File.WriteAllText(markerPath, "deadbeef\n");

        var guard = new GitLockGuard(_runner, new FakeGitProcessDetector());

        var interrupted = guard.DetectInterruptedState(repo);

        Assert.NotNull(interrupted);
        Assert.Equal(expectedKind, interrupted!.Kind);
        Assert.True(File.Exists(markerPath), "Interrupted-state markers must never be deleted automatically.");
    }

    [Fact]
    public void DetectInterruptedState_RebaseMergeDirectory_IsRecognizedAsRebase()
    {
        var repo = CreateBareGitDir("rebase-merge-dir-repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git", "rebase-merge"));

        var guard = new GitLockGuard(_runner, new FakeGitProcessDetector());
        var interrupted = guard.DetectInterruptedState(repo);

        Assert.NotNull(interrupted);
        Assert.Equal(GitInterruptedStateKind.Rebase, interrupted!.Kind);
    }

    [Fact]
    public async Task EnsureWritableAsync_InterruptedMerge_BlocksBeforeTouchingAnythingElse()
    {
        var repo = await InitRealGitRepoAsync("interrupted-merge-blocks-write");
        File.WriteAllText(Path.Combine(repo, ".git", "MERGE_HEAD"), "deadbeef\n");

        var guard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = false });

        var result = await guard.EnsureWritableAsync(repo);

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("Merge", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureWritableAsync_NoLockNoInterruptedState_ReturnsNullAndProceedsCleanly()
    {
        var repo = await InitRealGitRepoAsync("clean-write-gate-repo");
        var guard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = false });

        var result = await guard.EnsureWritableAsync(repo);

        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureWritableAsync_OrphanedLock_AutoRemovesAndProceeds()
    {
        var repo = await InitRealGitRepoAsync("auto-remove-write-gate-repo");
        var lockPath = Path.Combine(repo, ".git", "index.lock");
        WriteBackdatedFile(lockPath, TimeSpan.FromSeconds(30));

        var guard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = false });

        var result = await guard.EnsureWritableAsync(repo);

        Assert.Null(result); // safe to proceed
        Assert.False(File.Exists(lockPath));
    }

    // ── Windows-style path with spaces (validated here on this OS as a proxy;
    //    real Windows-path verification is left to the Windows session). ──────

    [Fact]
    public async Task RemoveOrphanedLockAsync_RepositoryPathContainsSpaces_WorksCorrectly()
    {
        var repo = await InitRealGitRepoAsync("path with spaces and (parens)");
        var lockPath = Path.Combine(repo, ".git", "index.lock");
        WriteBackdatedFile(lockPath, TimeSpan.FromSeconds(30));

        var guard = new GitLockGuard(_runner, new FakeGitProcessDetector { ActiveProcessPresent = false });
        var result = await guard.RemoveOrphanedLockAsync(repo);

        Assert.Equal(GitLockStatus.RemovedSuccessfully, result.Status);
        Assert.False(File.Exists(lockPath));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string CreateBareGitDir(string name)
    {
        var repo = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        return repo;
    }

    private async Task<string> InitRealGitRepoAsync(string name)
    {
        var repo = Path.Combine(_root, name);
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["config", "user.email", "tests@example.invalid"]);
        await Git(repo, ["config", "user.name", "AI GitHub Manager Tests"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "initial");
        await Git(repo, ["add", "README.md"]);
        await Git(repo, ["commit", "-m", "Initial"]);
        return repo;
    }

    private static void WriteBackdatedLockFile(string repositoryPath, TimeSpan age)
    {
        var lockPath = Path.Combine(repositoryPath, ".git", "index.lock");
        WriteBackdatedFile(lockPath, age);
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
