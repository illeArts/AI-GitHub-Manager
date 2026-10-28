using System.Text.Json;
using System.Text.RegularExpressions;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Remote;
using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Projects;

public enum ImportError { InvalidRemote, InvalidPath, UnsafePath, OccupiedTarget, ForeignRepository,
    GitUnavailable, AuthenticationRequired, RemoteUnavailable, GitFailed, UnsafeUpdate, FileAccess, Persistence }
public sealed class ProjectImportException(ImportError error) : Exception(error.ToString())
{
    public ImportError Error { get; } = error;
}
public enum ImportChoice { Link, Update, Cancel }
public sealed record ImportInspection(string Path, bool ExistingRepository, string Branch = "",
    string Upstream = "", bool Dirty = false, int Ahead = 0, int Behind = 0, bool OperationInProgress = false)
{
    public bool CanUpdate => ExistingRepository && !Dirty && !OperationInProgress && Ahead == 0 && Upstream.StartsWith("origin/", StringComparison.Ordinal);
}
public sealed record ImportResult(string Path, string RemoteUrl, string RepositoryId);

/// <summary>Injectable process boundary; production uses the existing gh login, never a PAT.</summary>
public interface IProjectImportCommands
{
    Task<CommandResult> GitAsync(string[] arguments, string directory, CancellationToken token);
    Task<CommandResult> GhAsync(string[] arguments, CancellationToken token);
}
public sealed class ProjectImportCommands(CommandRunner runner) : IProjectImportCommands
{
    public Task<CommandResult> GitAsync(string[] arguments, string directory, CancellationToken token)
        => runner.RunIsolatedGitAsync(["-c", "core.hooksPath=", .. arguments], directory, token);
    public Task<CommandResult> GhAsync(string[] arguments, CancellationToken token)
        => runner.RunAsync(GitHubCliService.GhExe, arguments, cancellationToken: token);
}

/// <summary>Clone/link state machine. No resets, automatic pulls, overwriting moves or recursive cleanup.</summary>
public sealed class ProjectImportService(IProjectImportCommands commands)
{
    public static string CanonicalRemote(string remote)
    {
        var match = Regex.Match(remote.Trim(), @"^(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)([A-Za-z0-9-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase);
        if (!match.Success || match.Groups[2].Value is "." or "..")
            throw new ProjectImportException(ImportError.InvalidRemote);
        return RemoteUrlNormalizer.Parse(remote.Trim())!.CanonicalHttpsUrl;
    }

    public static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ProjectImportException(ImportError.InvalidPath);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Path.GetDirectoryName(full) is null || File.Exists(full))
            throw new ProjectImportException(ImportError.InvalidPath);
        for (var current = new DirectoryInfo(full); current is not null; current = current.Parent)
        {
            // Attributes also detect dangling links; Exists alone does not.
            if (current.LinkTarget is not null || (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)))
                throw new ProjectImportException(ImportError.UnsafePath);
            if (current.FullName != full && (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git"))))
                throw new ProjectImportException(ImportError.UnsafePath);
        }
        return full;
    }

    public async Task<ImportInspection> InspectAsync(string remote, string path, CancellationToken token = default)
    {
        remote = CanonicalRemote(remote);
        path = ValidatePath(path);
        if (!Directory.Exists(path) || !Directory.EnumerateFileSystemEntries(path).Any())
            return new(path, false);
        var gitPath = Path.Combine(path, ".git");
        // Linked worktrees and external git directories require a separate ownership contract.
        if (!Directory.Exists(gitPath)) throw new ProjectImportException(ImportError.OccupiedTarget);
        await Task.Run(() => CheckGitDirectory(gitPath, token), token);
        var top = await Git(["rev-parse", "--show-toplevel"], path, token);
        if (!PathsEqual(top.StandardOutput.Trim(), path)) throw new ProjectImportException(ImportError.UnsafePath);
        var origin = await Git(["remote", "get-url", "origin"], path, token);
        try
        {
            if (!CanonicalRemote(origin.StandardOutput.Trim()).Equals(remote, StringComparison.OrdinalIgnoreCase))
                throw new ProjectImportException(ImportError.ForeignRepository);
        }
        catch (ProjectImportException) { throw new ProjectImportException(ImportError.ForeignRepository); }
        var status = await Git(["status", "--porcelain", "--untracked-files=all", "--ignored"], path, token);
        var branch = await Git(["branch", "--show-current"], path, token);
        var upstream = await commands.GitAsync(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], path, token);
        int ahead = 0, behind = 0;
        if (upstream.Success)
        {
            var counts = await Git(["rev-list", "--left-right", "--count", "HEAD...@{u}"], path, token);
            var values = counts.StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 2 || !int.TryParse(values[0], out ahead) || !int.TryParse(values[1], out behind))
                throw new ProjectImportException(ImportError.GitFailed);
        }
        var inProgress = File.Exists(Path.Combine(gitPath, "index.lock")) ||
            new GitLockGuard(new CommandRunner()).DetectInterruptedState(path) is not null;
        return new(path, true, branch.StandardOutput.Trim(), upstream.Success ? upstream.StandardOutput.Trim() : "",
            !string.IsNullOrWhiteSpace(status.StandardOutput), ahead, behind, inProgress);
    }

    public async Task<ImportResult?> ImportAsync(string remote, string target,
        Func<ImportInspection, Task<ImportChoice>> choose, IProgress<string>? progress = null,
        CancellationToken token = default)
    {
        try
        {
            remote = CanonicalRemote(remote);
            target = ValidatePath(target);
            using var repositoryLease = await RepositoryLockService.Shared.AcquireAsync(target, token);
            progress?.Report("Preflight");
            var parent = Path.GetDirectoryName(target)!;
            var existingParent = parent;
            while (!Directory.Exists(existingParent)) existingParent = Path.GetDirectoryName(existingParent)!;
            if (!(await commands.GitAsync(["--version"], existingParent, token)).Success)
                throw new ProjectImportException(ImportError.GitUnavailable);
            if (!(await commands.GhAsync(["auth", "status", "--hostname", "github.com"], token)).Success)
                throw new ProjectImportException(ImportError.AuthenticationRequired);
            var slug = remote["https://github.com/".Length..^4];
            var metadata = await commands.GhAsync(["repo", "view", slug, "--json", "id,url"], token);
            if (!metadata.Success) throw new ProjectImportException(ImportError.RemoteUnavailable);
            using var document = JsonDocument.Parse(metadata.StandardOutput);
            var id = document.RootElement.GetProperty("id").GetString();
            var actual = document.RootElement.GetProperty("url").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(id) || !CanonicalRemote(actual).Equals(remote, StringComparison.OrdinalIgnoreCase))
                throw new ProjectImportException(ImportError.RemoteUnavailable);
            var inspection = await InspectAsync(remote, target, token);
            if (inspection.ExistingRepository)
            {
                progress?.Report("Existing");
                var choice = await choose(inspection);
                token.ThrowIfCancellationRequested();
                if (choice == ImportChoice.Cancel) return null;
                inspection = await InspectAsync(remote, target, token);
                if (!inspection.ExistingRepository) throw new ProjectImportException(ImportError.UnsafePath);
                if (choice == ImportChoice.Update)
                {
                    if (!inspection.CanUpdate) throw new ProjectImportException(ImportError.UnsafeUpdate);
                    progress?.Report("Updating");
                    await Git(["fetch", "--no-tags", "origin"], target, token);
                    inspection = await InspectAsync(remote, target, token);
                    if (!inspection.CanUpdate) throw new ProjectImportException(ImportError.UnsafeUpdate);
                    var ff = await commands.GitAsync(["merge-base", "--is-ancestor", "HEAD", "@{u}"], target, token);
                    if (!ff.Success) throw new ProjectImportException(ImportError.UnsafeUpdate);
                    ValidatePath(target);
                    await Git(["-c", "merge.autostash=false", "merge", "--ff-only", "--no-edit", "--no-overwrite-ignore", "@{u}"], target, token);
                }
            }
            else
            {
                progress?.Report("Cloning");
                ValidatePath(target);
                Directory.CreateDirectory(parent);
                ValidatePath(target);
                // Git may remove its own destination on failure. Never let that destination be the user's folder.
                var staging = Path.Combine(parent, ".ai-github-import-" + Guid.NewGuid().ToString("N"));
                await Git(["clone", "--", remote, staging], parent, token);
                await InspectAsync(remote, staging, token);
                token.ThrowIfCancellationRequested();
                ValidatePath(target);
                if (Directory.Exists(target))
                {
                    if (Directory.EnumerateFileSystemEntries(target).Any())
                        throw new ProjectImportException(ImportError.OccupiedTarget);
                    // Non-recursive deletion fails if another process populated the empty target.
                    Directory.Delete(target, recursive: false);
                }
                ValidatePath(target);
                Directory.Move(staging, target); // Atomic no-replace publication on the same volume.
            }
            token.ThrowIfCancellationRequested();
            await InspectAsync(remote, target, token);
            progress?.Report("Saving");
            return new(target, remote, id);
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectImportException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw new ProjectImportException(ImportError.FileAccess); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new ProjectImportException(ImportError.RemoteUnavailable); }
    }

    private async Task<CommandResult> Git(string[] args, string path, CancellationToken token)
    {
        var result = await commands.GitAsync(args, path, token);
        if (!result.Success) throw new ProjectImportException(ImportError.GitFailed);
        return result; // Raw Git/gh output is deliberately never exposed to UI/logs.
    }
    private static bool PathsEqual(string left, string right) => Path.GetFullPath(left).Equals(Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void CheckGitDirectory(string gitPath, CancellationToken token)
    {
        var pending = new Stack<string>();
        pending.Push(gitPath);
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
                throw new ProjectImportException(ImportError.UnsafePath);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new ProjectImportException(ImportError.UnsafePath);
                if (attributes.HasFlag(FileAttributes.Directory)) pending.Push(entry);
            }
        }
        if (File.Exists(Path.Combine(gitPath, "commondir")) || File.Exists(Path.Combine(gitPath, "objects", "info", "alternates")))
            throw new ProjectImportException(ImportError.UnsafePath);
    }
}
