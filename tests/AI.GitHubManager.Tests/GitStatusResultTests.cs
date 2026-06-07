using AI.GitHubManager.Core.Git;
using Xunit;

namespace AI.GitHubManager.Tests;

public class GitStatusResultTests
{
    [Fact]
    public void IsRepository_FalseWhenNotARepo()
    {
        var r = new GitStatusResult(false, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), "Kein Repo");
        Assert.False(r.IsRepository);
        Assert.Equal("Kein Repo", r.ErrorMessage);
    }

    [Fact]
    public void IsRepository_TrueWhenValidRepo()
    {
        var r = new GitStatusResult(true, "main", "https://github.com/a/b.git", string.Empty, Array.Empty<string>(), null);
        Assert.True(r.IsRepository);
        Assert.Null(r.ErrorMessage);
    }

    [Fact]
    public void ChangedFiles_EmptyWhenNoChanges()
    {
        var r = new GitStatusResult(true, "main", "https://github.com/a/b.git", string.Empty, Array.Empty<string>(), null);
        Assert.Empty(r.ChangedFiles);
    }

    [Fact]
    public void ChangedFiles_ReflectsPassedArray()
    {
        var files = new[] { "M src/App.cs", "A src/New.cs", "D src/Old.cs" };
        var r     = new GitStatusResult(true, "main", "origin", "...", files, null);
        Assert.Equal(3,            r.ChangedFiles.Count);
        Assert.Equal("M src/App.cs", r.ChangedFiles[0]);
        Assert.Equal("D src/Old.cs", r.ChangedFiles[2]);
    }

    [Fact]
    public void BranchAndRemote_StoredCorrectly()
    {
        var r = new GitStatusResult(true, "feature/my-branch", "https://github.com/owner/repo.git", string.Empty, Array.Empty<string>(), null);
        Assert.Equal("feature/my-branch",                 r.Branch);
        Assert.Equal("https://github.com/owner/repo.git", r.RemoteOrigin);
    }
}
