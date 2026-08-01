namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Best-effort check for whether an active git process appears to still be
/// working against a given repository, used before ever touching a
/// <c>.git/index.lock</c> file that looks orphaned.
///
/// Implementations MUST fail closed: if the check cannot be performed
/// reliably for any reason (access denied, unsupported platform, unexpected
/// exception), they must return <c>true</c> (assume a process is active)
/// rather than risk a false negative that could lead to deleting a lock a
/// real git process still owns. Callers rely on this fail-closed contract.
/// </summary>
public interface IGitProcessDetector
{
    /// <param name="normalizedRepositoryPath">Full, normalized repository path
    /// (see <see cref="RepositoryLockService.NormalizeKey"/>).</param>
    bool IsGitProcessActiveFor(string normalizedRepositoryPath);
}
