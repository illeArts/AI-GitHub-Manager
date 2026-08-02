using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Real, non-mocked integration tests for the advanced/dangerous Git
/// operations added in Meilenstein 4 (Teil B2). These call the actual git
/// binary against throwaway temp repositories — the same pattern as
/// <see cref="GitServiceIntegrationTests"/> — so they prove the safe
/// argument-list plumbing genuinely works, not just that a mock was called.
/// </summary>
public sealed class GitServiceAdvancedOperationsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-tests-advanced", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public GitServiceAdvancedOperationsTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task CleanAsync_RemovesUntrackedFiles_KeepsCommittedFiles()
    {
        var repo = CreatePath("repo-clean");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "tracked.txt"), "kept");
        await Git(repo, ["add", "tracked.txt"]);
        await Git(repo, ["commit", "-m", "Initial"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "untracked.txt"), "gone");

        var service = new GitService(_runner);
        var result = await service.CleanAsync(repo);

        Assert.True(result.Success, result.CombinedOutput);
        Assert.True(File.Exists(Path.Combine(repo, "tracked.txt")));
        Assert.False(File.Exists(Path.Combine(repo, "untracked.txt")));
    }

    [Fact]
    public async Task HardResetAsync_DiscardsUncommittedChanges()
    {
        var repo = CreatePath("repo-hard-reset");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "file.txt"), "original");
        await Git(repo, ["add", "file.txt"]);
        await Git(repo, ["commit", "-m", "Initial"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "file.txt"), "modified locally");

        var service = new GitService(_runner);
        var result = await service.HardResetAsync(repo, "HEAD");

        Assert.True(result.Success, result.CombinedOutput);
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(repo, "file.txt")));
    }

    [Fact]
    public async Task ResetAsync_KeepsChangesAsUnstaged()
    {
        var repo = CreatePath("repo-reset");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "a.txt"), "a");
        await Git(repo, ["add", "a.txt"]);
        await Git(repo, ["commit", "-m", "first"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "b.txt"), "b");
        await Git(repo, ["add", "b.txt"]);
        await Git(repo, ["commit", "-m", "second"]);

        var service = new GitService(_runner);
        var result = await service.ResetAsync(repo, "HEAD~1");

        Assert.True(result.Success, result.CombinedOutput);
        // b.txt content remains on disk (unstaged), but the second commit is gone.
        Assert.True(File.Exists(Path.Combine(repo, "b.txt")));
        var status = await Git(repo, ["status", "--porcelain"]);
        Assert.Contains("b.txt", status.StandardOutput);
    }

    [Fact]
    public async Task CherryPickAsync_AppliesCommitFromAnotherBranch()
    {
        var repo = CreatePath("repo-cherry-pick");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "base.txt"), "base");
        await Git(repo, ["add", "base.txt"]);
        await Git(repo, ["commit", "-m", "base"]);

        await Git(repo, ["checkout", "-b", "feature"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "feature.txt"), "feature");
        await Git(repo, ["add", "feature.txt"]);
        await Git(repo, ["commit", "-m", "feature commit"]);
        var featureCommit = (await Git(repo, ["rev-parse", "HEAD"])).StandardOutput.Trim();

        await Git(repo, ["checkout", "main"]);

        var service = new GitService(_runner);
        var result = await service.CherryPickAsync(repo, featureCommit);

        Assert.True(result.Success, result.CombinedOutput);
        Assert.True(File.Exists(Path.Combine(repo, "feature.txt")));
    }

    [Fact]
    public async Task RebaseAsync_ReplaysCommitsOntoNewBase()
    {
        var repo = CreatePath("repo-rebase");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "base.txt"), "base");
        await Git(repo, ["add", "base.txt"]);
        await Git(repo, ["commit", "-m", "base"]);

        await Git(repo, ["checkout", "-b", "feature"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "feature.txt"), "feature");
        await Git(repo, ["add", "feature.txt"]);
        await Git(repo, ["commit", "-m", "feature commit"]);

        await Git(repo, ["checkout", "main"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "main-only.txt"), "main");
        await Git(repo, ["add", "main-only.txt"]);
        await Git(repo, ["commit", "-m", "main-only commit"]);

        await Git(repo, ["checkout", "feature"]);
        var service = new GitService(_runner);
        var result = await service.RebaseAsync(repo, "main");

        Assert.True(result.Success, result.CombinedOutput);
        Assert.True(File.Exists(Path.Combine(repo, "main-only.txt")));
        Assert.True(File.Exists(Path.Combine(repo, "feature.txt")));
    }

    [Theory]
    [InlineData("-D")]
    [InlineData("")]
    [InlineData("main branch")]
    public async Task HardResetAsync_RejectsUnsafeTargetRef_WithoutCallingGit(string unsafeRef)
    {
        var repo = CreatePath("repo-unsafe-ref");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await ConfigureIdentityAsync(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "a.txt"), "a");
        await Git(repo, ["add", "a.txt"]);
        await Git(repo, ["commit", "-m", "a"]);

        var service = new GitService(_runner);
        var result = await service.HardResetAsync(repo, unsafeRef);

        Assert.False(result.Success);
        // File must be untouched — the rejected call never reached git.
        Assert.Equal("a", await File.ReadAllTextAsync(Path.Combine(repo, "a.txt")));
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
