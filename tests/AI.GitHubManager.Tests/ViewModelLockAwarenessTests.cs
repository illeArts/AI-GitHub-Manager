using System.Diagnostics;
using AI.GitHubManager.App.ViewModels;
using AI.GitHubManager.Core.Projects;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Regression test for the real Windows Block 4.1 QA failure: an orphaned
/// <c>.git/index.lock</c> was correctly detected by <see cref="AI.GitHubManager.Core.Git.GitLockGuard"/>
/// (the repair button appeared), but "Umgebung prüfen" still printed
/// "Alle kritischen Checks bestanden. Push möglich." with no mention of the lock
/// at all, because <c>RefreshGitLockAvailability()</c> only appended to <c>Log</c>
/// when an <em>interrupted-state</em> marker was present — never for a plain
/// orphaned/active-process lock status. This test exercises the exact
/// <see cref="MainWindowViewModel.CheckEnvironmentCommand"/> path end-to-end
/// against a real temporary git repository and asserts the lock is always
/// reflected in the log, closing that gap.
/// </summary>
public sealed class ViewModelLockAwarenessTests
{
    [Fact]
    public async Task CheckEnvironmentAsync_AlwaysSurfacesDetectedLock_EvenWhenPreflightTextClaimsSuccess()
    {
        var repoDir = Directory.CreateTempSubdirectory("aigithubmanager-locktest-").FullName;
        try
        {
            RunGit(repoDir, "init", "-q");
            RunGit(repoDir, "config", "user.email", "test@example.com");
            RunGit(repoDir, "config", "user.name", "Test");

            // Simulate an orphaned index.lock: present, empty, and older than
            // GitLockGuard's MinimumOrphanAge (5s) so it is treated as
            // OrphanedRemovable rather than "too new to tell".
            var lockPath = Path.Combine(repoDir, ".git", "index.lock");
            File.WriteAllText(lockPath, string.Empty);
            File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow - TimeSpan.FromSeconds(30));

            var vm = new MainWindowViewModel();
            var project = new ManagedProject
            {
                Name = "lock-test",
                LinuxPath = repoDir,
                WindowsPath = repoDir,
                MacPath = repoDir,
            };

            // Overwrites whatever the fire-and-forget startup project load may have
            // selected (that code only ever assigns via `??=`, so this always wins).
            vm.SelectedProject = project;

            var checkEnvironment = (RelayCommand)vm.CheckEnvironmentCommand;
            await checkEnvironment.ExecuteAsync(null);

            Assert.True(vm.CanRemoveOrphanedGitLock,
                "An orphaned lock, old enough and with no active git process for this repo, must enable the repair button.");

            Assert.Contains(".git/index.lock", vm.Log, StringComparison.Ordinal);
            Assert.Contains("verwaist", vm.Log, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(repoDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
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
}
