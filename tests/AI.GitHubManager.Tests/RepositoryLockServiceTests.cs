using AI.GitHubManager.Core.Git;
using Xunit;

namespace AI.GitHubManager.Tests;

public sealed class RepositoryLockServiceTests
{
    [Fact]
    public async Task AcquireAsync_ConcurrentCallsForSameRepository_AreSerialized()
    {
        var service = new RepositoryLockService();
        var repo = Path.Combine(Path.GetTempPath(), "ai-github-manager-lock-serial-test");
        Directory.CreateDirectory(repo);

        var concurrentCount = 0;
        var maxObservedConcurrency = 0;
        var gate = new object();

        async Task SimulatedWriteOperationAsync()
        {
            using var _ = await service.AcquireAsync(repo);

            lock (gate)
            {
                concurrentCount++;
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, concurrentCount);
            }

            // Simulate a multi-step git operation (stash, pull, restore, ...) taking some time.
            await Task.Delay(50);

            lock (gate)
            {
                concurrentCount--;
            }
        }

        // Fire several "writing git operations" for the same repo at once.
        var tasks = Enumerable.Range(0, 8).Select(_ => SimulatedWriteOperationAsync());
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxObservedConcurrency);
    }

    [Fact]
    public async Task AcquireAsync_DifferentRepositories_RunConcurrently()
    {
        var service = new RepositoryLockService();
        var repoA = Path.Combine(Path.GetTempPath(), "ai-github-manager-lock-parallel-a");
        var repoB = Path.Combine(Path.GetTempPath(), "ai-github-manager-lock-parallel-b");
        Directory.CreateDirectory(repoA);
        Directory.CreateDirectory(repoB);

        var bothRunningAtOnce = new TaskCompletionSource<bool>();
        var aStarted = new TaskCompletionSource<bool>();
        var bStarted = new TaskCompletionSource<bool>();

        async Task RunAsync(string repo, TaskCompletionSource<bool> started, TaskCompletionSource<bool> other)
        {
            using var _ = await service.AcquireAsync(repo);
            started.TrySetResult(true);
            await Task.WhenAny(other.Task, Task.Delay(2000));
            bothRunningAtOnce.TrySetResult(true);
        }

        var taskA = RunAsync(repoA, aStarted, bStarted);
        var taskB = RunAsync(repoB, bStarted, aStarted);

        await Task.WhenAll(taskA, taskB);

        var bothRanConcurrently = await bothRunningAtOnce.Task;
        Assert.True(bothRanConcurrently, "Two different repositories must not serialize against each other.");
    }

    [Fact]
    public async Task IsWriteInProgress_WhileGateHeld_ReturnsTrue_ThenFalseAfterRelease()
    {
        var service = new RepositoryLockService();
        var repo = Path.Combine(Path.GetTempPath(), "ai-github-manager-lock-inprogress-test");
        Directory.CreateDirectory(repo);

        Assert.False(service.IsWriteInProgress(repo));

        var handle = await service.AcquireAsync(repo);
        Assert.True(service.IsWriteInProgress(repo));

        handle.Dispose();
        Assert.False(service.IsWriteInProgress(repo));
    }

    [Fact]
    public async Task NormalizeKey_CaseDifference_MapsToTheSameLockKey()
    {
        // The underlying dictionary uses an OrdinalIgnoreCase comparer specifically so
        // that Windows' case-insensitive filesystem doesn't let two "different-looking"
        // paths to the same folder bypass serialization.
        var service = new RepositoryLockService();
        var repo = Path.Combine(Path.GetTempPath(), "ai-github-manager-lock-case-test", "MixedCaseRepo");
        Directory.CreateDirectory(repo);

        var handle = await service.AcquireAsync(repo);
        try
        {
            Assert.True(service.IsWriteInProgress(repo));
            Assert.True(service.IsWriteInProgress(repo.ToUpperInvariant()));
            Assert.True(service.IsWriteInProgress(repo.ToLowerInvariant()));
        }
        finally
        {
            handle.Dispose();
        }
    }

    [Fact]
    public async Task AcquireAsync_RepositoryPathWithSpaces_WorksCorrectly()
    {
        // Windows-style paths often contain spaces (e.g. "C:\Users\Jane Doe\My Projects\app").
        // Validated here on this OS as a proxy for the real Windows path; the actual
        // Windows-specific verification is left to a real Windows session.
        var service = new RepositoryLockService();
        var repo = Path.Combine(Path.GetTempPath(), "ai-github-manager-lock-space-test", "My Project (2026)");
        Directory.CreateDirectory(repo);

        using (var handle = await service.AcquireAsync(repo))
        {
            Assert.True(service.IsWriteInProgress(repo));
        }

        Assert.False(service.IsWriteInProgress(repo));
    }
}
