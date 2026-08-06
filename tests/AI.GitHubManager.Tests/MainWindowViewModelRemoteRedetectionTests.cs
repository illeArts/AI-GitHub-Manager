using AI.GitHubManager.App.ViewModels;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Data;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Covers "Remote erneut erkennen" at the ViewModel level: re-detecting an
/// unchanged remote must not prompt, re-detecting a changed remote must ask
/// for confirmation first, and declining that confirmation must never
/// silently overwrite an existing (e.g. manually-entered) link.
/// </summary>
public sealed class MainWindowViewModelRemoteRedetectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-tests-vm-redetect", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();
    private readonly string _storeFile = Path.GetTempFileName();

    public MainWindowViewModelRemoteRedetectionTests() => Directory.CreateDirectory(_root);

    /// <summary>
    /// Isolated per-test <see cref="JsonProjectStore"/> — see the identical helper (and its
    /// doc comment explaining the shared-file race it avoids) in
    /// MainWindowViewModelAdvancedOperationTests.NewViewModel.
    /// </summary>
    private MainWindowViewModel NewViewModel() =>
        new(new GitService(_runner), new JsonProjectStore(_storeFile));

    [Fact]
    public async Task RedetectRemote_UnchangedRemote_SavesWithoutAskingForConfirmation()
    {
        var repo = await CreateRepoAsync("https://github.com/illeArts-Finance/bullbear.git");
        var project = new ManagedProject
        {
            Name = "bullbear",
            RepositoryOwner = "illeArts-Finance",
            RepositoryName = "bullbear",
            RepositoryWebUrl = "https://github.com/illeArts-Finance/bullbear",
            RemoteSource = RemoteSource.GitOrigin,
            WindowsPath = repo,
            MacPath = repo,
            LinuxPath = repo,
        };

        var confirmationsAsked = 0;
        var vm = NewViewModel();
        vm.ConfirmYesNoFunc = (_, _) => { confirmationsAsked++; return Task.FromResult(true); };
        vm.Projects.Add(project);

        await ((RelayCommand<ManagedProject>)vm.RedetectRemoteCommand).ExecuteAsync(project);

        Assert.Equal(0, confirmationsAsked);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", project.RepositoryWebUrl);
    }

    [Fact]
    public async Task RedetectRemote_ChangedRemote_ConfirmedOverwrite_UpdatesLink()
    {
        var repo = await CreateRepoAsync("https://github.com/illeArts-Finance/bullbear.git");
        var project = new ManagedProject
        {
            Name = "bullbear",
            RepositoryOwner = "someone-else",
            RepositoryName = "old-name",
            RepositoryWebUrl = "https://github.com/someone-else/old-name",
            RemoteSource = RemoteSource.Manual,
            WindowsPath = repo,
            MacPath = repo,
            LinuxPath = repo,
        };

        var confirmationsAsked = 0;
        var vm = NewViewModel();
        vm.ConfirmYesNoFunc = (_, _) => { confirmationsAsked++; return Task.FromResult(true); };
        vm.Projects.Add(project);

        await ((RelayCommand<ManagedProject>)vm.RedetectRemoteCommand).ExecuteAsync(project);

        Assert.Equal(1, confirmationsAsked);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", project.RepositoryWebUrl);
        Assert.Equal("illeArts-Finance", project.RepositoryOwner);
        Assert.Equal(RemoteSource.GitOrigin, project.RemoteSource);
    }

    [Fact]
    public async Task RedetectRemote_ChangedRemote_DeclinedConfirmation_KeepsManualLinkUnchanged()
    {
        var repo = await CreateRepoAsync("https://github.com/illeArts-Finance/bullbear.git");
        var project = new ManagedProject
        {
            Name = "bullbear",
            RepositoryOwner = "someone-else",
            RepositoryName = "old-name",
            RepositoryWebUrl = "https://github.com/someone-else/old-name",
            RemoteSource = RemoteSource.Manual,
            WindowsPath = repo,
            MacPath = repo,
            LinuxPath = repo,
        };

        var vm = NewViewModel();
        // The user says "no" to overwriting their manual assignment.
        vm.ConfirmYesNoFunc = (_, _) => Task.FromResult(false);
        vm.Projects.Add(project);

        await ((RelayCommand<ManagedProject>)vm.RedetectRemoteCommand).ExecuteAsync(project);

        // Nothing here may be silently overwritten just because a different
        // remote was detected — the manual assignment must survive untouched.
        Assert.Equal("https://github.com/someone-else/old-name", project.RepositoryWebUrl);
        Assert.Equal("someone-else", project.RepositoryOwner);
        Assert.Equal("old-name", project.RepositoryName);
        Assert.Equal(RemoteSource.Manual, project.RemoteSource);
    }

    private async Task<string> CreateRepoAsync(string originUrl)
    {
        var repo = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        await _runner.RunAsync("git", ["init"], repo);
        await _runner.RunAsync("git", ["remote", "add", "origin", originUrl], repo);
        return repo;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, FileAttributes.Normal);

            Directory.Delete(_root, recursive: true);
        }

        if (File.Exists(_storeFile)) File.Delete(_storeFile);
    }
}
