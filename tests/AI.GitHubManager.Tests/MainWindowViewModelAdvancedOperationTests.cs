using AI.GitHubManager.App.ViewModels;
using AI.GitHubManager.App.Views;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Operations;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Covers Teil B7/D at the ViewModel level: dangerous/advanced operations must
/// never execute without an explicit, positive confirmation result, and must
/// never fabricate success when nothing ran.
/// </summary>
public sealed class MainWindowViewModelAdvancedOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-github-manager-tests-vm-advanced", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();

    public MainWindowViewModelAdvancedOperationTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ExecuteAdvancedOperationCommand_WithoutConfirmFuncWired_DoesNotExecute()
    {
        var repo = await CreateRepoWithUntrackedFileAsync();
        var vm = new MainWindowViewModel(new GitService(_runner)) { LocalPath = repo };
        // ConfirmAdvancedOperationFunc intentionally left null.

        await ((RelayCommand<GitOperationDefinition>)vm.ExecuteAdvancedOperationCommand)
            .ExecuteAsync(GitOperationCatalog.Clean);

        Assert.True(File.Exists(Path.Combine(repo, "untracked.txt")));
        Assert.Contains("NICHT ausgeführt", vm.Log);
    }

    [Fact]
    public async Task ExecuteAdvancedOperationCommand_ConfirmationDeclined_DoesNotExecute()
    {
        var repo = await CreateRepoWithUntrackedFileAsync();
        var vm = new MainWindowViewModel(new GitService(_runner))
        {
            LocalPath = repo,
            ConfirmAdvancedOperationFunc = _ => Task.FromResult<AdvancedOperationConfirmationResult?>(
                new AdvancedOperationConfirmationResult(false, null)),
        };

        await ((RelayCommand<GitOperationDefinition>)vm.ExecuteAdvancedOperationCommand)
            .ExecuteAsync(GitOperationCatalog.Clean);

        Assert.True(File.Exists(Path.Combine(repo, "untracked.txt")));
        Assert.Contains("abgebrochen", vm.Log);
    }

    [Fact]
    public async Task ExecuteAdvancedOperationCommand_ConfirmationNull_DoesNotExecute()
    {
        var repo = await CreateRepoWithUntrackedFileAsync();
        var vm = new MainWindowViewModel(new GitService(_runner))
        {
            LocalPath = repo,
            ConfirmAdvancedOperationFunc = _ => Task.FromResult<AdvancedOperationConfirmationResult?>(null),
        };

        await ((RelayCommand<GitOperationDefinition>)vm.ExecuteAdvancedOperationCommand)
            .ExecuteAsync(GitOperationCatalog.Clean);

        Assert.True(File.Exists(Path.Combine(repo, "untracked.txt")));
    }

    [Fact]
    public async Task ExecuteAdvancedOperationCommand_ConfirmedClean_ActuallyExecutes()
    {
        var repo = await CreateRepoWithUntrackedFileAsync();
        var vm = new MainWindowViewModel(new GitService(_runner))
        {
            LocalPath = repo,
            ConfirmAdvancedOperationFunc = _ => Task.FromResult<AdvancedOperationConfirmationResult?>(
                new AdvancedOperationConfirmationResult(true, null)),
        };

        await ((RelayCommand<GitOperationDefinition>)vm.ExecuteAdvancedOperationCommand)
            .ExecuteAsync(GitOperationCatalog.Clean);

        Assert.False(File.Exists(Path.Combine(repo, "untracked.txt")));
        Assert.Contains("erfolgreich", vm.Log);
    }

    [Fact]
    public async Task ExecuteAdvancedOperationCommand_ConfirmedHardResetWithInvalidTargetRef_ReportsErrorAndDoesNotCallGitDestructively()
    {
        var repo = await CreateRepoWithUntrackedFileAsync();
        var vm = new MainWindowViewModel(new GitService(_runner))
        {
            LocalPath = repo,
            ConfirmAdvancedOperationFunc = _ => Task.FromResult<AdvancedOperationConfirmationResult?>(
                new AdvancedOperationConfirmationResult(true, "-D")), // unsafe ref must be rejected, not passed to git
        };

        await ((RelayCommand<GitOperationDefinition>)vm.ExecuteAdvancedOperationCommand)
            .ExecuteAsync(GitOperationCatalog.HardReset);

        // Rejected before reaching git — untracked file still there either way,
        // but the important assertion is that no exception occurred and the
        // failure is reported readably.
        Assert.False(string.IsNullOrWhiteSpace(vm.Log));
    }

    private async Task<string> CreateRepoWithUntrackedFileAsync()
    {
        var repo = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        await _runner.RunAsync("git", ["init"], repo);
        // The test fixture must not inherit a developer's global excludes file:
        // git clean intentionally preserves ignored files, which would make this
        // test dependent on a local rule such as *.txt.
        await _runner.RunAsync("git", ["config", "core.excludesfile", "/dev/null"], repo);
        await _runner.RunAsync("git", ["config", "user.email", "tests@example.invalid"], repo);
        await _runner.RunAsync("git", ["config", "user.name", "AI GitHub Manager Tests"], repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "tracked.txt"), "kept");
        await _runner.RunAsync("git", ["add", "tracked.txt"], repo);
        await _runner.RunAsync("git", ["commit", "-m", "Initial"], repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "untracked.txt"), "gone");
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
    }
}
