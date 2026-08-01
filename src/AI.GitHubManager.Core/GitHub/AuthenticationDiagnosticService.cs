using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.GitHub;

/// <summary>
/// Orchestrates a full authentication diagnosis: calls `gh`, and — only when an
/// environment token is present and looks like it might be the problem — runs a
/// second `gh auth status` in a child process with GH_TOKEN/GITHUB_TOKEN removed,
/// to check whether a valid keyring login exists underneath.
///
/// The actual decision-making lives in <see cref="AuthenticationDiagnosisResolver"/>,
/// which is pure and unit-tested directly. This class only owns the process calls.
/// </summary>
public sealed class AuthenticationDiagnosticService
{
    public static readonly IReadOnlyList<string> DefaultScopes = new[] { "repo", "read:org" };
    public static readonly IReadOnlyList<string> WorkflowScopes = new[] { "repo", "read:org", "workflow" };

    private readonly GitHubCliService _gh;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public AuthenticationDiagnosticService(GitHubCliService gh, Func<string, string?>? environmentVariableReader = null)
    {
        _gh = gh;
        _getEnvironmentVariable = environmentVariableReader ?? Environment.GetEnvironmentVariable;
    }

    public async Task<AuthenticationDiagnosis> DiagnoseAsync(
        IReadOnlyList<string>? requiredScopes = null,
        CancellationToken cancellationToken = default)
    {
        requiredScopes ??= DefaultScopes;

        var ghVersion = await SafeRun(() => _gh.VersionAsync(), cancellationToken);
        if (ghVersion is null || !ghVersion.Success)
            return AuthenticationDiagnosisResolver.CliUnavailable();

        var ghTokenEnv = _getEnvironmentVariable("GH_TOKEN");
        var githubTokenEnv = _getEnvironmentVariable("GITHUB_TOKEN");
        bool hasEnvToken = !string.IsNullOrWhiteSpace(ghTokenEnv) || !string.IsNullOrWhiteSpace(githubTokenEnv);

        var primary = await SafeRun(() => _gh.AuthStatusAsync(), cancellationToken);
        if (primary is null)
            return AuthenticationDiagnosisResolver.CheckFailed("`gh auth status` konnte nicht ausgeführt werden.");

        CommandResult? cleaned = null;
        if (hasEnvToken)
        {
            var primaryInfo = GitHubAuthStatusParser.Parse(primary);
            bool primaryLooksGood = primary.Success
                                     && primaryInfo.LoggedIn
                                     && primaryInfo.ActiveAccount != false
                                     && !primaryInfo.HasTokenFailure;

            // Only pay for the second process call when the first one didn't
            // already succeed cleanly — this keeps the common case (valid token,
            // no token at all) fast.
            if (!primaryLooksGood)
                cleaned = await SafeRun(() => _gh.AuthStatusWithCleanedEnvironmentAsync(), cancellationToken);
        }

        return AuthenticationDiagnosisResolver.Resolve(primary, cleaned, ghTokenEnv, githubTokenEnv, requiredScopes);
    }

    private static async Task<CommandResult?> SafeRun(Func<Task<CommandResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
