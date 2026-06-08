using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

public sealed class GitServiceIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-tests", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public GitServiceIntegrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task GetStatusAsync_ReportsMissingOriginWithoutRejectingRepository()
    {
        var repo = CreatePath("repo-no-origin");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);

        var service = new GitService(_runner);

        var status = await service.GetStatusAsync(repo);

        Assert.True(status.IsRepository);
        Assert.Equal(string.Empty, status.RemoteOrigin);
        Assert.Equal("Remote 'origin' ist nicht gesetzt.", status.ErrorMessage);
    }

    [Fact]
    public async Task CommitAndPushAsync_PreservesCommitMessagesWithQuotes()
    {
        var remote = CreatePath("remote.git");
        var repo = CreatePath("repo-with-remote");
        Directory.CreateDirectory(repo);

        await Git(_root, ["init", "--bare", remote]);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);
        await Git(repo, ["config", "user.email", "tests@example.invalid"]);
        await Git(repo, ["config", "user.name", "AI GitHub Manager Tests"]);
        await Git(repo, ["remote", "add", "origin", remote]);

        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "initial");
        await Git(repo, ["add", "README.md"]);
        await Git(repo, ["commit", "-m", "Initial"]);
        await Git(repo, ["push", "-u", "origin", "main"]);

        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "changed");
        var service = new GitService(_runner);

        var result = await service.CommitAndPushAsync(repo, "Update \"quoted\" message");

        Assert.True(result.Success, result.CombinedOutput);

        var lastCommit = await Git(repo, ["log", "-1", "--pretty=%s"]);
        Assert.Equal("Update \"quoted\" message", lastCommit.StandardOutput.Trim());
    }

    [Fact]
    public async Task CommitAndPushAsync_SkipsWhenNoChangesAndNothingAhead()
    {
        var remote = CreatePath("remote-skip.git");
        var repo = CreatePath("repo-skip");
        Directory.CreateDirectory(repo);

        await Git(_root, ["init", "--bare", remote]);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);
        await Git(repo, ["config", "user.email", "tests@example.invalid"]);
        await Git(repo, ["config", "user.name", "AI GitHub Manager Tests"]);
        await Git(repo, ["remote", "add", "origin", remote]);
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "initial");
        await Git(repo, ["add", "README.md"]);
        await Git(repo, ["commit", "-m", "Initial"]);
        await Git(repo, ["push", "-u", "origin", "main"]);

        var service = new GitService(_runner);

        var result = await service.CommitAndPushAsync(repo, "No changes");

        Assert.True(result.Success, result.CombinedOutput);
        Assert.Contains("Commit und Push übersprungen", result.CombinedOutput);
    }

    [Fact]
    public async Task PullAsync_AddsMissingOriginFromProjectRemoteUrl()
    {
        var remote = CreatePath("remote-without-local-origin.git");
        var seed = CreatePath("seed-without-local-origin");
        var repo = CreatePath("repo-without-origin");

        await Git(_root, ["init", "--bare", remote]);
        await Git(_root, ["clone", remote, seed]);
        await ConfigureIdentityAsync(seed);
        await File.WriteAllTextAsync(Path.Combine(seed, "README.md"), "remote");
        await Git(seed, ["add", "README.md"]);
        await Git(seed, ["commit", "-m", "Initial"]);
        await Git(seed, ["branch", "-M", "main"]);
        await Git(seed, ["push", "-u", "origin", "main"]);

        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);

        var service = new GitService(_runner);
        var result = await service.PullAsync(repo, "main", remote);

        Assert.True(result.Success, result.CombinedOutput);
        Assert.Equal("remote", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));
    }

    [Fact]
    public async Task PullAsync_RecoversUntrackedOverwriteWhenLocalBranchHasNoCommits()
    {
        var remote = CreatePath("remote-untracked-overwrite.git");
        var seed = CreatePath("seed-untracked-overwrite");
        var repo = CreatePath("repo-untracked-overwrite");

        await Git(_root, ["init", "--bare", remote]);
        await Git(_root, ["clone", remote, seed]);
        await ConfigureIdentityAsync(seed);
        await File.WriteAllTextAsync(Path.Combine(seed, "README.md"), "remote");
        await Git(seed, ["add", "README.md"]);
        await Git(seed, ["commit", "-m", "Initial"]);
        await Git(seed, ["branch", "-M", "main"]);
        await Git(seed, ["push", "-u", "origin", "main"]);

        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["branch", "-M", "main"]);
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "local");

        var service = new GitService(_runner);
        var result = await service.PullAsync(repo, "main", remote);

        Assert.True(result.Success, result.CombinedOutput);
        Assert.Contains("Auto-Recovery ausgeführt", result.CombinedOutput);
        Assert.Equal("local", await File.ReadAllTextAsync(Path.Combine(repo, "README.md")));

        var status = await Git(repo, ["status", "--porcelain"]);
        Assert.Contains("M README.md", status.StandardOutput);
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
