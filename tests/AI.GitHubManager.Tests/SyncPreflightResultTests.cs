using AI.GitHubManager.Core.Diagnostics;
using Xunit;

namespace AI.GitHubManager.Tests;

public class SyncPreflightResultTests
{
    [Fact]
    public void CanPush_TrueWhenNoErrors()
    {
        var result = new SyncPreflightResult(new[]
        {
            new PreflightItem("Git",    PreflightSeverity.Ok,      "git 2.45"),
            new PreflightItem("Branch", PreflightSeverity.Warning, "Kein Branch")
        });
        Assert.True(result.CanPush);
    }

    [Fact]
    public void CanPush_FalseWhenAnyError()
    {
        var result = new SyncPreflightResult(new[]
        {
            new PreflightItem("Git",       PreflightSeverity.Ok,    "git 2.45"),
            new PreflightItem("Auth",      PreflightSeverity.Error, "Nicht eingeloggt.")
        });
        Assert.False(result.CanPush);
    }

    [Fact]
    public void HasWarnings_TrueWhenWarningPresent()
    {
        var result = new SyncPreflightResult(new[]
        {
            new PreflightItem("Changes", PreflightSeverity.Warning, "3 Dateien")
        });
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void HasWarnings_FalseWhenOnlyOkItems()
    {
        var result = new SyncPreflightResult(new[]
        {
            new PreflightItem("Git", PreflightSeverity.Ok, "2.45")
        });
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void ToLogText_ContainsAllLabels()
    {
        var result = new SyncPreflightResult(new[]
        {
            new PreflightItem("Git",  PreflightSeverity.Ok,    "2.45"),
            new PreflightItem("Auth", PreflightSeverity.Error, "Fehler")
        }, branch: "main", remoteOrigin: "https://github.com/owner/repo.git");

        var log = result.ToLogText();
        Assert.Contains("Git",    log);
        Assert.Contains("Auth",   log);
        Assert.Contains("main",   log);
        Assert.Contains("github", log);
        Assert.Contains("Push blockiert", log);
    }

    [Fact]
    public void ToLogText_ShowsPushPossibleWhenNoErrors()
    {
        var result = new SyncPreflightResult(new[]
        {
            new PreflightItem("Git", PreflightSeverity.Ok, "2.45")
        });
        Assert.Contains("Push möglich", result.ToLogText());
    }

    [Fact]
    public void BranchAndRemoteOrigin_ExposedCorrectly()
    {
        var result = new SyncPreflightResult(
            Enumerable.Empty<PreflightItem>(),
            branch: "develop",
            remoteOrigin: "https://github.com/a/b.git");

        Assert.Equal("develop", result.Branch);
        Assert.Equal("https://github.com/a/b.git", result.RemoteOrigin);
    }
}
