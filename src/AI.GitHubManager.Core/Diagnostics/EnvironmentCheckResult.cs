using AI.GitHubManager.Core.GitHub;

namespace AI.GitHubManager.Core.Diagnostics;

public sealed record EnvironmentCheckResult(
    bool GitInstalled,
    bool GitHubCliInstalled,
    bool GitHubAuthenticated,
    string GitVersion,
    string GitHubCliVersion,
    string GitHubAuthOutput,
    AuthenticationDiagnosis? Authentication = null);
