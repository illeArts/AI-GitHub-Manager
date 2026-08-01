using System.Collections.Concurrent;

namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Ensures at most one writing git operation (pull, commit+push, merge,
/// stash, restore, ...) runs at a time <em>per repository</em>, across every
/// service in this process (<see cref="GitService"/>, <see cref="SafePullService"/>).
/// Keyed by the repository's normalized full path, so two different
/// <c>ManagedProject</c> entries that happen to point at the same folder
/// still serialize against each other.
///
/// This is an in-process safeguard only. It cannot and does not try to
/// coordinate with other applications, terminals, or IDEs that might also be
/// touching the same repository — see <see cref="GitLockGuard"/> and
/// <see cref="IGitProcessDetector"/> for that half of the problem (detecting
/// whether an <em>external</em> git process is active before ever touching
/// a stale-looking <c>index.lock</c>).
/// </summary>
public sealed class RepositoryLockService
{
    /// <summary>Process-wide shared instance. Used by default so unrelated
    /// <see cref="GitService"/>/<see cref="SafePullService"/> instances still
    /// serialize against each other for the same repository path.</summary>
    public static readonly RepositoryLockService Shared = new();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Normalizes a repository path so equivalent paths (different
    /// casing on Windows, trailing separators, relative segments) map to the
    /// same lock key.</summary>
    public static string NormalizeKey(string repositoryPath)
    {
        var full = Path.GetFullPath(repositoryPath);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Waits until no other writing operation is running for this repository,
    /// then returns a disposable that releases the gate. Always use with
    /// <c>using</c>/<c>await using</c> so the gate is released even on
    /// exceptions or cancellation.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(repositoryPath);
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(gate);
    }

    /// <summary>True while a writing operation for this repository is currently in flight
    /// (i.e. some caller is holding the gate acquired via <see cref="AcquireAsync"/>).</summary>
    public bool IsWriteInProgress(string repositoryPath)
    {
        var key = NormalizeKey(repositoryPath);
        return _gates.TryGetValue(key, out var gate) && gate.CurrentCount == 0;
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _gate;
        private int _disposed;

        public Releaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _gate.Release();
        }
    }
}
