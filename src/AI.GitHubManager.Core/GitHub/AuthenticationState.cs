namespace AI.GitHubManager.Core.GitHub;

/// <summary>
/// Structured result of a GitHub authentication diagnosis. Replaces the old
/// "did `gh auth status` exit with 0?" check, which cannot tell the difference
/// between "not logged in at all" and "a bad GH_TOKEN/GITHUB_TOKEN is hiding a
/// perfectly valid keyring login".
/// </summary>
public enum AuthenticationState
{
    /// <summary>Logged in via some other verified source (e.g. Git Credential Manager).</summary>
    Authenticated,

    /// <summary>Logged in via the GitHub CLI's own secure keyring/credential store.</summary>
    AuthenticatedViaKeyring,

    /// <summary>Logged in using a valid GH_TOKEN/GITHUB_TOKEN environment variable.</summary>
    AuthenticatedViaEnvironmentToken,

    /// <summary>An environment token is set and invalid, and no other valid login exists.</summary>
    InvalidEnvironmentToken,

    /// <summary>
    /// An invalid GH_TOKEN/GITHUB_TOKEN environment variable is hiding an otherwise
    /// valid keyring login. This is the reference bug this feature fixes.
    /// </summary>
    EnvironmentTokenOverridesValidKeyring,

    /// <summary>No valid authentication source could be found at all.</summary>
    NotAuthenticated,

    /// <summary>Authenticated, but one or more required OAuth scopes are missing.</summary>
    MissingRequiredScopes,

    /// <summary>The GitHub CLI (gh) could not be found or executed.</summary>
    GitHubCliUnavailable,

    /// <summary>The diagnosis itself failed unexpectedly (e.g. unreadable process output).</summary>
    AuthenticationCheckFailed
}
