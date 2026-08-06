using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Core.Remote;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Real, end-to-end coverage for the "Auf GitHub öffnen" feature: a real local
/// git repository (via <c>git init</c> + <c>git remote add origin ...</c> in a
/// temp directory) is inspected by <see cref="RemoteDetectionService"/>, which
/// must report the *real* remote — never a URL fabricated from a username.
///
/// The bullbear regression this guards against (from the original bug report):
/// a local project named "bullbear" whose real remote is the ORGANIZATION repo
/// https://github.com/illeArts-Finance/bullbear.git must resolve to
/// https://github.com/illeArts-Finance/bullbear — and must NEVER resolve to
/// https://github.com/illeArts/bullbear (the logged-in personal account name
/// combined with the local folder/project name).
/// </summary>
public sealed class RemoteDetectionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-remote-detect-tests", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public RemoteDetectionServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task DetectAsync_OrganizationHttpsRemote_ResolvesToRealOrgUrl_NeverToLoggedInUser()
    {
        var repo = CreatePath("bullbear");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        // The real remote: an ORGANIZATION repo, not the personal account "illeArts".
        await Git(repo, ["remote", "add", "origin", "https://github.com/illeArts-Finance/bullbear.git"]);

        var service = new RemoteDetectionService(new GitService(_runner));
        var result = await service.DetectAsync(repo);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("illeArts-Finance", result.Owner);
        Assert.Equal("bullbear", result.Repository);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", result.WebUrl);

        // The core bug this whole feature exists to prevent: never combine the
        // currently-authenticated gh username with the local project name.
        Assert.NotEqual("https://github.com/illeArts/bullbear", result.WebUrl);
    }

    [Fact]
    public async Task DetectAsync_PersonalHttpsRemote_Resolves()
    {
        var repo = CreatePath("personal-https");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["remote", "add", "origin", "https://github.com/illeArts/AndreAIAgent.git"]);

        var result = await new RemoteDetectionService(new GitService(_runner)).DetectAsync(repo);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("illeArts", result.Owner);
        Assert.Equal("AndreAIAgent", result.Repository);
        Assert.Equal("https://github.com/illeArts/AndreAIAgent", result.WebUrl);
    }

    [Fact]
    public async Task DetectAsync_OrganizationSshRemote_Resolves()
    {
        var repo = CreatePath("org-ssh");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["remote", "add", "origin", "git@github.com:illeArts-Finance/bullbear.git"]);

        var result = await new RemoteDetectionService(new GitService(_runner)).DetectAsync(repo);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("illeArts-Finance", result.Owner);
        Assert.Equal("bullbear", result.Repository);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", result.WebUrl);
    }

    [Fact]
    public async Task DetectAsync_PersonalSshRemote_Resolves()
    {
        var repo = CreatePath("personal-ssh");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["remote", "add", "origin", "git@github.com:illeArts/AndreAIAgent.git"]);

        var result = await new RemoteDetectionService(new GitService(_runner)).DetectAsync(repo);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("illeArts", result.Owner);
        Assert.Equal("AndreAIAgent", result.Repository);
    }

    [Fact]
    public async Task DetectAsync_RemoteWithoutDotGitSuffix_Resolves()
    {
        var repo = CreatePath("no-dot-git");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);
        await Git(repo, ["remote", "add", "origin", "https://github.com/illeArts-Finance/bullbear"]);

        var result = await new RemoteDetectionService(new GitService(_runner)).DetectAsync(repo);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", result.WebUrl);
    }

    [Fact]
    public async Task DetectAsync_MissingOrigin_FailsWithoutFabricatingUrl()
    {
        var repo = CreatePath("no-origin");
        Directory.CreateDirectory(repo);
        await Git(repo, ["init"]);

        var result = await new RemoteDetectionService(new GitService(_runner)).DetectAsync(repo);

        Assert.False(result.Success);
        Assert.Null(result.WebUrl);
        Assert.Null(result.Owner);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task DetectAsync_NotAGitRepository_Fails()
    {
        var folder = CreatePath("just-a-folder");
        Directory.CreateDirectory(folder);

        var result = await new RemoteDetectionService(new GitService(_runner)).DetectAsync(folder);

        Assert.False(result.Success);
        Assert.Null(result.WebUrl);
    }

    private string CreatePath(string name) => Path.Combine(_root, name);

    private async Task Git(string workingDirectory, string[] arguments)
    {
        var result = await _runner.RunAsync("git", arguments, workingDirectory);
        Assert.True(result.Success, result.CombinedOutput);
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

public class RemoteUrlNormalizerManualEntryTests
{
    [Fact]
    public void ParseManualEntry_FullHttpsUrl_Parses()
    {
        var info = RemoteUrlNormalizer.ParseManualEntry("https://github.com/illeArts-Finance/bullbear");
        Assert.NotNull(info);
        Assert.Equal("illeArts-Finance", info!.Owner);
        Assert.Equal("bullbear", info.Repository);
    }

    [Fact]
    public void ParseManualEntry_OwnerRepoShorthand_ParsesAsGithubCom()
    {
        var info = RemoteUrlNormalizer.ParseManualEntry("illeArts-Finance/bullbear");
        Assert.NotNull(info);
        Assert.Equal("github.com", info!.Host);
        Assert.Equal("illeArts-Finance", info.Owner);
        Assert.Equal("bullbear", info.Repository);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", info.WebUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url at all")]
    [InlineData("ftp://example.com/owner/repo")]
    [InlineData("owner/repo/extra/segment")]
    public void ParseManualEntry_InvalidInput_ReturnsNull(string input)
    {
        Assert.Null(RemoteUrlNormalizer.ParseManualEntry(input));
    }
}

public class ManagedProjectGitHubLinkTests
{
    [Fact]
    public void HasGitHubLink_FalseByDefault()
    {
        var project = new ManagedProject();
        Assert.False(project.HasGitHubLink);
        Assert.Equal(RemoteSource.Unknown, project.RemoteSource);
    }

    [Fact]
    public void HasGitHubLink_TrueOnceWebUrlSet()
    {
        var project = new ManagedProject { RepositoryWebUrl = "https://github.com/illeArts-Finance/bullbear" };
        Assert.True(project.HasGitHubLink);
    }

    [Fact]
    public void EditingLink_OverwritesPreviousManualEntry()
    {
        var project = new ManagedProject
        {
            RepositoryOwner = "oldOwner",
            RepositoryName = "oldRepo",
            RepositoryWebUrl = "https://github.com/oldOwner/oldRepo",
            RemoteSource = RemoteSource.Manual
        };

        // Simulate "GitHub-Link bearbeiten …" applying a new manual entry.
        var edited = RemoteUrlNormalizer.ParseManualEntry("https://github.com/illeArts-Finance/bullbear")!;
        project.RepositoryOwner = edited.Owner;
        project.RepositoryName = edited.Repository;
        project.RepositoryWebUrl = edited.WebUrl;

        Assert.Equal("illeArts-Finance", project.RepositoryOwner);
        Assert.Equal("bullbear", project.RepositoryName);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", project.RepositoryWebUrl);
    }
}
