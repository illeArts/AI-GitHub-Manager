using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

public class CommandResultTests
{
    [Fact]
    public void Success_TrueWhenExitCodeZero()
    {
        var r = new CommandResult(0, "ok", string.Empty, "git", "--version");
        Assert.True(r.Success);
    }

    [Fact]
    public void Success_FalseWhenExitCodeNonZero()
    {
        var r = new CommandResult(1, string.Empty, "error", "git", "push");
        Assert.False(r.Success);
    }

    [Fact]
    public void CombinedOutput_ContainsBothStreams()
    {
        var r = new CommandResult(0, "stdout-text", "stderr-text", "git", "--version");
        Assert.Contains("stdout-text", r.CombinedOutput);
        Assert.Contains("stderr-text", r.CombinedOutput);
    }

    [Fact]
    public void CombinedOutput_OnlyStdoutWhenStderrEmpty()
    {
        var r = new CommandResult(0, "only-stdout", string.Empty, "git", "--version");
        Assert.Equal("only-stdout", r.CombinedOutput);
    }

    [Fact]
    public void CombinedOutput_OnlyStderrWhenStdoutEmpty()
    {
        var r = new CommandResult(1, string.Empty, "only-stderr", "git", "push");
        Assert.Equal("only-stderr", r.CombinedOutput);
    }

    [Theory]
    [InlineData(0,  true)]
    [InlineData(1,  false)]
    [InlineData(-1, false)]
    [InlineData(128, false)]
    public void Success_OnlyTrueForExitCodeZero(int exitCode, bool expected)
    {
        var r = new CommandResult(exitCode, string.Empty, string.Empty, "git", "status");
        Assert.Equal(expected, r.Success);
    }
}
