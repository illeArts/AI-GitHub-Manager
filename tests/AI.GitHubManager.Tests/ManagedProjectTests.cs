using AI.GitHubManager.Core.Projects;
using Xunit;

namespace AI.GitHubManager.Tests;

public class ManagedProjectTests
{
    [Fact]
    public void NewProject_HasUniqueId()
    {
        var p1 = new ManagedProject();
        var p2 = new ManagedProject();
        Assert.NotEqual(p1.Id, p2.Id);
    }

    [Fact]
    public void NewProject_IdIsNonEmpty()
    {
        var p = new ManagedProject();
        Assert.False(string.IsNullOrWhiteSpace(p.Id));
    }

    [Fact]
    public void NewProject_DefaultBranchIsMain()
    {
        var p = new ManagedProject();
        Assert.Equal("main", p.DefaultBranch);
    }

    [Fact]
    public void NewProject_CreatedAtIsRecent()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var p      = new ManagedProject();
        var after  = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(p.CreatedAt, before, after);
    }

    [Fact]
    public void GetPathForCurrentPlatform_ReturnsNonNullValue()
    {
        var p = new ManagedProject
        {
            WindowsPath = @"C:\projects\test",
            MacPath     = "/mac/projects/test",
            LinuxPath   = "/linux/projects/test"
        };
        Assert.NotNull(p.GetPathForCurrentPlatform());
    }

    [Fact]
    public void GetPathForCurrentPlatform_ReturnsCorrectWindowsPath()
    {
        if (!OperatingSystem.IsWindows()) return; // Skip on non-Windows

        var p = new ManagedProject { WindowsPath = @"C:\projects\test" };
        Assert.Equal(@"C:\projects\test", p.GetPathForCurrentPlatform());
    }

    [Fact]
    public void GetPathForCurrentPlatform_ReturnsCorrectMacPath()
    {
        if (!OperatingSystem.IsMacOS()) return; // Skip on non-macOS

        var p = new ManagedProject { MacPath = "/Users/test/projects" };
        Assert.Equal("/Users/test/projects", p.GetPathForCurrentPlatform());
    }
}
