using System.Diagnostics;
using AI.GitHubManager.App.ViewModels;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Projects;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Regression test for the real Windows Block 4.1 QA failure: an orphaned
/// <c>.git/index.lock</c> was correctly detected by <see cref="GitLockGuard"/>
/// (the repair button appeared), but "Umgebung prüfen" still printed
/// "Alle kritischen Checks bestanden. Push möglich." with no mention of the lock
/// at all, because <c>RefreshGitLockAvailability()</c> only appended to <c>Log</c>
/// when an <em>interrupted-state</em> marker was present — never for a plain
/// orphaned/active-process lock status.
///
/// These tests exercise the exact <see cref="MainWindowViewModel.CheckEnvironmentCommand"/>
/// path end-to-end against a real temporary git repository, but inject a
/// <see cref="FakeGitProcessDetector"/> via the internal GitService-injecting
/// constructor so the outcome never depends on real, possibly concurrently-running
/// git.exe processes on the machine or test run (an earlier version of this test
/// used the real platform process detector and failed non-deterministically under
/// `dotnet test` when other tests' git.exe subprocesses were still exiting).
/// </summary>
public sealed class ViewModelLockAwarenessTests : IDisposable
{
    private readonly string _repoDir = Directory.CreateTempSubdirectory("aigithubmanager-locktest-").FullName;

    [Fact]
    public async Task CheckEnvironmentAsync_AlwaysSurfacesDetectedLock_EvenWhenPreflightTextClaimsSuccess()
    {
        CreateOrphanedLock();

        var vm = BuildViewModel(activeProcessPresent: false);

        var checkEnvironment = (RelayCommand)vm.CheckEnvironmentCommand;
        await checkEnvironment.ExecuteAsync(null);

        Assert.True(vm.CanRemoveOrphanedGitLock,
            "An orphaned lock, old enough and with no active git process for this repo (per the injected fake detector), must enable the repair button.");

        Assert.Contains(".git/index.lock", vm.Log, StringComparison.Ordinal);
        Assert.Contains("verwaist", vm.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckEnvironmentAsync_DoesNotOfferRemoval_WhenAnActiveProcessIsDetected()
    {
        CreateOrphanedLock();

        var vm = BuildViewModel(activeProcessPresent: true);

        var checkEnvironment = (RelayCommand)vm.CheckEnvironmentCommand;
        await checkEnvironment.ExecuteAsync(null);

        Assert.False(vm.CanRemoveOrphanedGitLock,
            "A lock must never be offered for automatic removal while an active git process is detected (or cannot be safely excluded).");

        Assert.Contains(".git/index.lock", vm.Log, StringComparison.Ordinal);
        Assert.Contains("aktiver Git-Prozess", vm.Log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds a ViewModel wired to a GitService/GitLockGuard using a
    /// deterministic <see cref="FakeGitProcessDetector"/> instead of the real,
    /// platform-specific (WMI/`Process.GetProcessesByName`) detector.</summary>
    private MainWindowViewModel BuildViewModel(bool activeProcessPresent)
    {
        var runner = new CommandRunner();
        var detector = new FakeGitProcessDetector { ActiveProcessPresent = activeProcessPresent };
        var lockGuard = new GitLockGuard(runner, detector);
        var gitService = new GitService(runner, RepositoryLockService.Shared, lockGuard);

        var vm = new MainWindowViewModel(gitService);
        var project = new ManagedProject
        {
            Name = "lock-test",
            LinuxPath = _repoDir,
            WindowsPath = _repoDir,
            MacPath = _repoDir,
        };

        // Overwrites whatever the fire-and-forget startup project load may have
        // selected (that code only ever assigns via `??=`, so this always wins).
        vm.SelectedProject = project;
        return vm;
    }

    private void CreateOrphanedLock()
    {
        RunGit(_repoDir, "init", "-q");
        RunGit(_repoDir, "config", "user.email", "test@example.com");
        RunGit(_repoDir, "config", "user.name", "Test");

        // Present, empty, and older than GitLockGuard's MinimumOrphanAge (5s) so it
        // is treated as OrphanedRemovable rather than "too new to tell" — set via an
        // explicit, controlled timestamp rather than relying on real wall-clock delay.
        var lockPath = Path.Combine(_repoDir, ".git", "index.lock");
        File.WriteAllText(lockPath, string.Empty);
        File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow - TimeSpan.FromSeconds(30));
    }

    private static void RunGit(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoDir, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
