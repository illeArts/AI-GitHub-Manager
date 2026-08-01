namespace AI.GitHubManager.Core.GitHub;

/// <summary>
/// Structured view of a single `gh auth status` invocation. Parsed from the
/// command's combined stdout/stderr text — never contains a raw token value.
/// </summary>
public sealed record GitHubAuthStatusInfo(
    bool CommandSucceeded,
    bool LoggedIn,
    string? Account,
    string? Source,
    bool SourceIsKeyring,
    bool? ActiveAccount,
    IReadOnlyList<string> Scopes,
    bool HasTokenFailure,
    string? FailedTokenVariable,
    string RawOutput);
