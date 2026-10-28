using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Data;
using AI.GitHubManager.App.ViewModels;
using Xunit;

namespace AI.GitHubManager.Tests;

public sealed class ProjectImportTests : IDisposable
{
    private const string Remote = "https://github.com/example/private.project.git";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-import-tests", Guid.NewGuid().ToString("N"));
    private readonly CommandRunner _runner = new();
    private readonly Commands _commands;
    private readonly ProjectImportService _service;
    private string Target => Path.Combine(_root, "selected project");
    private string Origin => Path.Combine(_root, "origin.git");
    private string Seed => Path.Combine(_root, "seed");

    public ProjectImportTests()
    {
        _root = ProjectImportService.ValidatePath(_root);
        Directory.CreateDirectory(_root);
        _commands = new Commands(_runner, Origin);
        _service = new ProjectImportService(_commands);
    }

    private async Task Init(bool empty = false)
    {
        await Git(_root, "init", "--bare", "--initial-branch=main", Origin);
        await Git(_root, "clone", Origin, Seed);
        await Identity(Seed);
        if (!empty) await Advance("initial");
    }
    private async Task Advance(string text)
    {
        await File.WriteAllTextAsync(Path.Combine(Seed, "README.md"), text);
        await Git(Seed, "add", ".");
        await Git(Seed, "commit", "-m", text);
        await Git(Seed, "push", "-u", "origin", "main");
    }
    private Task<ImportResult?> Import(ImportChoice choice = ImportChoice.Link, CancellationToken token = default)
        => _service.ImportAsync(Remote, Target, _ => Task.FromResult(choice), token: token);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clone_UsesExactMissingOrEmptyTarget_AndPersistsLink(bool exists)
    {
        await Init();
        if (exists) Directory.CreateDirectory(Target);
        var result = await Import();
        Assert.NotNull(result);
        Assert.Equal(Target, result.Path);
        Assert.Equal("R_private", result.RepositoryId);
        Assert.Equal("initial", await File.ReadAllTextAsync(Path.Combine(Target, "README.md")));
        Assert.True((await new GitService(_runner).GetStatusAsync(Target)).IsRepository);
        Assert.Contains(_commands.GhCalls, a => a.SequenceEqual(new[] { "auth", "status", "--hostname", "github.com" }));
        Assert.DoesNotContain(_commands.GhCalls, a => a.Contains("setup-git"));
        Assert.DoesNotContain(_commands.GitCalls.SelectMany(x => x), x => x.Contains("token", StringComparison.OrdinalIgnoreCase));
        var project = new ManagedProject { Owner = "example", Name = "private.project", RemoteUrl = result.RemoteUrl, GitHubRepositoryId = result.RepositoryId };
        if (OperatingSystem.IsWindows()) project.WindowsPath = result.Path;
        else if (OperatingSystem.IsMacOS()) project.MacPath = result.Path;
        else project.LinuxPath = result.Path;
        var store = new JsonProjectStore(Path.Combine(_root, "projects.json"));
        await store.SaveAsync([project]);
        var loaded = Assert.Single(await store.LoadAsync());
        Assert.Equal(Target, loaded.GetPathForCurrentPlatform());
        Assert.Equal("R_private", loaded.GitHubRepositoryId);
    }

    [Fact]
    public async Task ImportedProject_StatusPullCommitPush_WorkIncludingFirstCommitInEmptyRemote()
    {
        await Init(empty: true);
        await Import();
        await Identity(Target);
        await File.WriteAllTextAsync(Path.Combine(Target, "README.md"), "first");
        var git = new GitService(_runner);
        var pushed = await git.CommitAndPushAsync(Target, "first commit");
        Assert.True(pushed.Success, pushed.CombinedOutput);
        await Git(Seed, "pull", "origin", "main");
        await Advance("second");
        var pulled = await git.PullAsync(Target, CancellationToken.None);
        Assert.True(pulled.Success, pulled.CombinedOutput);
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(Target, "README.md")));
        Assert.Empty((await git.GetStatusAsync(Target)).ChangedFiles);
    }

    [Fact]
    public async Task LinkDirtyMatchingRepository_DoesNotChangeFilesOrFetch()
    {
        await Init(); await Import();
        var readme = Path.Combine(Target, "README.md");
        await File.WriteAllTextAsync(readme, "local work");
        var snapshot = Snapshot(Target);
        var config = await File.ReadAllBytesAsync(Path.Combine(Target, ".git", "config"));
        _commands.GitCalls.Clear();
        ImportInspection? inspected = null;
        var result = await _service.ImportAsync(Remote, Target, s => { inspected = s; return Task.FromResult(ImportChoice.Link); });
        Assert.NotNull(result);
        Assert.True(inspected!.Dirty);
        Assert.False(inspected.CanUpdate);
        EqualSnapshot(snapshot, Snapshot(Target));
        Assert.Equal(config, await File.ReadAllBytesAsync(Path.Combine(Target, ".git", "config")));
        Assert.DoesNotContain(_commands.GitCalls, a => a.Contains("fetch") || a.Contains("merge") || a.Contains("clone"));
    }

    [Fact]
    public async Task ExistingRepository_RequiresDecision_AndCancellationLeavesEverythingUnchanged()
    {
        await Init(); await Import(); await Advance("remote advance");
        var snapshot = Snapshot(Target);
        Assert.Null(await Import(ImportChoice.Cancel));
        EqualSnapshot(snapshot, Snapshot(Target));
        await Import(ImportChoice.Link);
        EqualSnapshot(snapshot, Snapshot(Target));
        Assert.Equal("initial", await File.ReadAllTextAsync(Path.Combine(Target, "README.md")));
    }

    [Fact]
    public async Task Update_FastForwardsOnlyAfterExplicitChoice()
    {
        await Init(); await Import(); await Advance("remote advance");
        await Import(ImportChoice.Update);
        Assert.Equal("remote advance", await File.ReadAllTextAsync(Path.Combine(Target, "README.md")));
        Assert.DoesNotContain(_commands.GitCalls.SelectMany(a => a), a => a is "reset" or "--force" or "--rebase");
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("ignored")]
    [InlineData("divergent")]
    [InlineData("no-upstream")]
    [InlineData("detached")]
    [InlineData("lock")]
    [InlineData("interrupted")]
    public async Task Update_BlocksUnsafeStates_PreservesLocalFiles(string state)
    {
        await Init(); await Import();
        if (state == "dirty") await File.WriteAllTextAsync(Path.Combine(Target, "README.md"), "local");
        if (state == "ignored")
        {
            await File.WriteAllTextAsync(Path.Combine(Target, ".git", "info", "exclude"), "ignored.txt\n");
            await File.WriteAllTextAsync(Path.Combine(Target, "ignored.txt"), "never overwrite");
        }
        if (state == "divergent")
        {
            await Identity(Target);
            await File.WriteAllTextAsync(Path.Combine(Target, "local.txt"), "local commit");
            await Git(Target, "add", "."); await Git(Target, "commit", "-m", "local");
            await Advance("remote divergence");
        }
        if (state == "no-upstream") await Git(Target, "branch", "--unset-upstream");
        if (state == "detached") await Git(Target, "checkout", "--detach");
        if (state == "lock") await File.WriteAllTextAsync(Path.Combine(Target, ".git", "index.lock"), "keep");
        if (state == "interrupted") await File.WriteAllTextAsync(Path.Combine(Target, ".git", "MERGE_HEAD"), "keep");
        var files = Snapshot(Target);
        var error = await Assert.ThrowsAsync<ProjectImportException>(() => Import(ImportChoice.Update));
        Assert.Equal(ImportError.UnsafeUpdate, error.Error);
        EqualSnapshot(files, Snapshot(Target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OccupiedOrForeignTarget_IsByteIdenticalAfterRefusal(bool repository)
    {
        await Init();
        Directory.CreateDirectory(Target);
        if (repository)
        {
            await Git(Target, "init");
            await Git(Target, "remote", "add", "origin", "git@github.com:other/foreign.git");
        }
        await File.WriteAllBytesAsync(Path.Combine(Target, "precious.bin"), [0, 1, 255, 42]);
        var snapshot = Snapshot(Target);
        var error = await Assert.ThrowsAsync<ProjectImportException>(() => Import());
        Assert.Equal(repository ? ImportError.ForeignRepository : ImportError.OccupiedTarget, error.Error);
        EqualSnapshot(snapshot, Snapshot(Target));
    }

    [Fact]
    public async Task TargetPopulatedDuringClone_IsPreservedAndNotLinked()
    {
        await Init();
        Directory.CreateDirectory(Target);
        _commands.AfterClone = async () => await File.WriteAllTextAsync(Path.Combine(Target, "concurrent.txt"), "keep");
        var error = await Assert.ThrowsAsync<ProjectImportException>(() => Import());
        Assert.Equal(ImportError.OccupiedTarget, error.Error);
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(Target, "concurrent.txt")));
        Assert.False(Directory.Exists(Path.Combine(Target, ".git")));
    }

    [Fact]
    public async Task ChangesMadeWhileChoosingUpdate_AreRechecked()
    {
        await Init(); await Import();
        var error = await Assert.ThrowsAsync<ProjectImportException>(() => _service.ImportAsync(Remote, Target, async _ =>
        {
            await File.WriteAllTextAsync(Path.Combine(Target, "README.md"), "new local change");
            return ImportChoice.Update;
        }));
        Assert.Equal(ImportError.UnsafeUpdate, error.Error);
        Assert.Equal("new local change", await File.ReadAllTextAsync(Path.Combine(Target, "README.md")));
    }

    [Theory]
    [InlineData("auth", ImportError.AuthenticationRequired)]
    [InlineData("remote", ImportError.RemoteUnavailable)]
    [InlineData("git", ImportError.GitUnavailable)]
    [InlineData("clone", ImportError.GitFailed)]
    [InlineData("permission", ImportError.FileAccess)]
    public async Task PreflightAndCloneErrors_DoNotPublishTargetOrExposeSecrets(string failure, ImportError expected)
    {
        await Init(); _commands.Failure = failure;
        var error = await Assert.ThrowsAsync<ProjectImportException>(() => Import());
        Assert.Equal(expected, error.Error);
        Assert.DoesNotContain("ghp_secret", error.ToString());
        Assert.False(Directory.Exists(Target));
    }

    [Fact]
    public async Task CancellationDuringClone_DoesNotPublishOrDeleteUserFiles()
    {
        await Init();
        using var cts = new CancellationTokenSource();
        _commands.AfterClone = () => { cts.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Import(token: cts.Token));
        Assert.False(Directory.Exists(Target));
        Assert.NotEmpty(Directory.GetDirectories(_root, ".ai-github-import-*"));
    }

    [Fact]
    public async Task RejectsNestedRepositoryDestinationAndExternalGitDirectory()
    {
        await Init(); await Import();
        var error = await Assert.ThrowsAsync<ProjectImportException>(() => _service.InspectAsync(Remote, Path.Combine(Target, "nested")));
        Assert.Equal(ImportError.UnsafePath, error.Error);
        await File.WriteAllTextAsync(Path.Combine(Target, ".git", "commondir"), "../elsewhere");
        error = await Assert.ThrowsAsync<ProjectImportException>(() => _service.InspectAsync(Remote, Target));
        Assert.Equal(ImportError.UnsafePath, error.Error);
    }

    [Theory]
    [InlineData("https://github.com/a/b.git", "https://github.com/a/b.git")]
    [InlineData("git@github.com:a/re.po.git", "https://github.com/a/re.po.git")]
    [InlineData("ssh://git@github.com/a/b", "https://github.com/a/b.git")]
    public void RemoteIdentity_CanonicalizesHttpsAndSsh(string input, string expected)
        => Assert.Equal(expected, ProjectImportService.CanonicalRemote(input));

    [Theory]
    [InlineData("https://token@github.com/a/b")]
    [InlineData("https://github.com.evil/a/b")]
    [InlineData("https://github.com/a/../b")]
    [InlineData("--upload-pack=evil")]
    [InlineData("file:///tmp/repo")]
    public void RemoteIdentity_RejectsCredentialsTraversalAndCommandInjection(string input)
        => Assert.Throws<ProjectImportException>(() => ProjectImportService.CanonicalRemote(input));

    [Fact]
    public async Task SymlinkOrJunctionDestination_IsRejectedWithoutTouchingSource()
    {
        var actual = Path.Combine(_root, "real files");
        Directory.CreateDirectory(actual);
        await File.WriteAllTextAsync(Path.Combine(actual, "keep.txt"), "keep");
        if (OperatingSystem.IsWindows())
        {
            var junction = await _runner.RunAsync("cmd.exe", $"/c mklink /J \"{Target}\" \"{actual}\"");
            Assert.True(junction.Success, junction.CombinedOutput);
        }
        else Directory.CreateSymbolicLink(Target, actual);
        try
        {
            var error = Assert.Throws<ProjectImportException>(() => ProjectImportService.ValidatePath(Target));
            Assert.Equal(ImportError.UnsafePath, error.Error);
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(actual, "keep.txt")));
            error = Assert.Throws<ProjectImportException>(() => ProjectImportService.ValidatePath(Path.Combine(Target, "child")));
            Assert.Equal(ImportError.UnsafePath, error.Error);
        }
        finally { Directory.Delete(Target); }
    }

    [Fact]
    public async Task CancellationStopsGitAndItsChildrenPromptly()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _runner.RunAsync("git",
            ["-c", "alias.import-test-wait=!sleep 30", "import-test-wait"], _root, cancellation.Token));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Localization_CoversEveryStateAndError_WithMatchingKeysAndFormatArguments()
    {
        var english = ImportText.Catalog("en");
        var german = ImportText.Catalog("de");
        Assert.Equal(english.Keys.Order(), german.Keys.Order());
        foreach (var key in english.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(german[key]));
            Assert.Equal(System.Text.RegularExpressions.Regex.Matches(english[key], @"\{\d+\}").Select(m => m.Value).Order(),
                System.Text.RegularExpressions.Regex.Matches(german[key], @"\{\d+\}").Select(m => m.Value).Order());
        }
        foreach (var error in Enum.GetNames<ImportError>()) Assert.True(english.ContainsKey(error));
        foreach (var state in new[] { "Preflight", "Cloning", "Updating", "Existing", "Saving", "Cancelled" }) Assert.True(english.ContainsKey(state));
        Assert.Equal(english["Import"], ImportText.Get("Import", "future-language"));
    }

    private Task Identity(string path) => ConfigureIdentity(path);

    [Fact]
    public void Authentication_IsScopedToNetworkCommands_WithoutChangingStoredRemoteOrConfig()
    {
        var args = GitHubCredentialScope.Apply(["clone", "--", Remote, "target with spaces"]);
        Assert.Contains("credential.https://github.com.helper=", args);
        Assert.Contains(args, a => a.Contains("auth git-credential"));
        Assert.Contains("url.https://github.com/.insteadOf=git@github.com:", args);
        Assert.Equal(new[] { "clone", "--", Remote, "target with spaces" }, args[^4..]);
        Assert.Equal(new[] { "status", "--porcelain" }, GitHubCredentialScope.Apply(["status", "--porcelain"]));
        Assert.Equal(args, GitHubCredentialScope.Apply(args));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("clone-failure")]
    [InlineData("save-failure")]
    [InlineData("cancel")]
    public async Task ViewModel_PersistsOnlySuccessfulImports_AndKeepsPreviousMappingOnFailure(string scenario)
    {
        await Init();
        var storePath = Path.Combine(_root, "viewmodel-projects.json");
        var store = new JsonProjectStore(storePath);
        var project = new ManagedProject { Owner = "old-owner", Name = "chosen", RemoteUrl = Remote, RepositoryWebUrl = "https://github.com/old/project" };
        if (OperatingSystem.IsWindows()) project.WindowsPath = "C:\\previous";
        else if (OperatingSystem.IsMacOS()) project.MacPath = "/previous";
        else project.LinuxPath = "/previous";
        var previous = project.GetPathForCurrentPlatform();
        await store.SaveAsync([project]);
        if (scenario == "save-failure")
        {
            File.Delete(storePath);
            Directory.CreateDirectory(storePath); // deterministic atomic-publication failure on every platform
        }
        var vm = new MainWindowViewModel(null, store, _service, initialize: false);
        vm.Projects.Add(project);
        vm.SelectedProject = project;
        vm.ImportTargetFunc = (_, _) => Task.FromResult<string?>(scenario == "cancel" ? null : Target);
        vm.ImportChoiceFunc = _ => Task.FromResult(ImportChoice.Link);
        if (scenario == "clone-failure") _commands.Failure = "clone";
        await vm.ImportProjectAsync();
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsImporting);
        if (scenario == "success")
        {
            var loaded = Assert.Single(await store.LoadAsync());
            Assert.Equal(Target, vm.LocalPath);
            Assert.Equal(Target, loaded.GetPathForCurrentPlatform());
            Assert.Equal("example", loaded.RepositoryOwner);
            Assert.Equal("private.project", loaded.RepositoryName);
            Assert.Equal("https://github.com/example/private.project", loaded.RepositoryWebUrl);
            Assert.Equal("R_private", loaded.GitHubRepositoryId);
        }
        else
        {
            Assert.Equal(previous, project.GetPathForCurrentPlatform());
            Assert.Equal(previous, vm.LocalPath);
            Assert.Equal("old-owner", project.Owner);
            Assert.Equal("https://github.com/old/project", project.RepositoryWebUrl);
            Assert.Empty(project.GitHubRepositoryId);
            if (scenario == "save-failure") Assert.True(File.Exists(Path.Combine(Target, "README.md")));
            else Assert.Equal(previous, Assert.Single(await store.LoadAsync()).GetPathForCurrentPlatform());
        }
    }
    private async Task ConfigureIdentity(string path)
    {
        await Git(path, "config", "user.email", "test@example.invalid");
        await Git(path, "config", "user.name", "Import Test");
    }
    private async Task<CommandResult> Git(string path, params string[] arguments)
    {
        var result = await _runner.RunAsync("git", arguments, path);
        Assert.True(result.Success, result.CombinedOutput);
        return result;
    }
    private static Dictionary<string, byte[]> Snapshot(string path) => Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .Where(p => !Path.GetRelativePath(path, p).Split(Path.DirectorySeparatorChar).Contains(".git"))
        .ToDictionary(p => Path.GetRelativePath(path, p), File.ReadAllBytes);
    private static void EqualSnapshot(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var file in expected) Assert.Equal(file.Value, actual[file.Key]);
    }
    public void Dispose()
    {
        // Only the unique test-owned temporary root is removed.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private sealed class Commands(CommandRunner runner, string origin) : IProjectImportCommands
    {
        public string Failure { get; set; } = "";
        public Func<Task>? AfterClone { get; set; }
        public List<string[]> GitCalls { get; } = [];
        public List<string[]> GhCalls { get; } = [];
        private static CommandResult Ok(string output = "") => new(0, output, "", "test", "");
        private static CommandResult Fail() => new(1, "", "network failure ghp_secret", "test", "");
        public Task<CommandResult> GhAsync(string[] args, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            GhCalls.Add(args);
            if ((Failure == "auth" && args.Contains("status")) || (Failure == "remote" && args.Contains("view")) ||
                (Failure == "credentials" && args.Contains("status"))) return Task.FromResult(Fail());
            return Task.FromResult(args.Contains("view") ? Ok("{\"id\":\"R_private\",\"url\":\"https://github.com/example/private.project\"}") : Ok());
        }
        public async Task<CommandResult> GitAsync(string[] args, string directory, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            GitCalls.Add(args);
            if (Failure == "git" && args.Contains("--version")) return Fail();
            if (args[0] == "clone")
            {
                if (Failure == "clone") return Fail();
                if (Failure == "permission") throw new UnauthorizedAccessException("secret path");
                var clone = await runner.RunAsync("git", new[] { "clone", "--", origin, args[^1] }, directory, token);
                if (AfterClone is not null) await AfterClone();
                return clone;
            }
            var result = await runner.RunAsync("git", args, directory, token);
            // Local bare origin stands in for a private GitHub transport; all Git operations remain real.
            return args.SequenceEqual(new[] { "remote", "get-url", "origin" }) && result.StandardOutput.Trim() == origin
                ? Ok(Remote) : result;
        }
    }
}
