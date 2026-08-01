using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.GitHub;

/// <summary>
/// Pure decision logic that turns one or two `gh auth status` results (the
/// second one run with GH_TOKEN/GITHUB_TOKEN removed from a child process'
/// environment only) into a single <see cref="AuthenticationDiagnosis"/>.
///
/// This class has no side effects and starts no processes, so it can be
/// unit-tested directly with hand-built <see cref="CommandResult"/> values —
/// no fake process runner needed.
/// </summary>
public static class AuthenticationDiagnosisResolver
{
    /// <summary>
    /// Resolves the final diagnosis.
    /// </summary>
    /// <param name="primary">Result of `gh auth status` in the current (real) environment.</param>
    /// <param name="cleaned">
    /// Result of `gh auth status` run again with GH_TOKEN/GITHUB_TOKEN removed for that one
    /// child process only. Null when no environment token was present, so the second pass
    /// was never necessary.
    /// </param>
    /// <param name="ghTokenEnv">Current value of GH_TOKEN, or null/empty if unset.</param>
    /// <param name="githubTokenEnv">Current value of GITHUB_TOKEN, or null/empty if unset.</param>
    /// <param name="requiredScopes">OAuth scopes required for the action being performed.</param>
    public static AuthenticationDiagnosis Resolve(
        CommandResult primary,
        CommandResult? cleaned,
        string? ghTokenEnv,
        string? githubTokenEnv,
        IReadOnlyList<string> requiredScopes)
    {
        var primaryInfo = GitHubAuthStatusParser.Parse(primary);

        bool hasGhToken = !string.IsNullOrWhiteSpace(ghTokenEnv);
        bool hasGithubToken = !string.IsNullOrWhiteSpace(githubTokenEnv);
        bool hasEnvToken = hasGhToken || hasGithubToken;

        // gh itself gives GH_TOKEN priority over GITHUB_TOKEN, so when both are
        // set, GH_TOKEN is the one actually in effect.
        string? relevantVariable = hasGhToken ? "GH_TOKEN" : hasGithubToken ? "GITHUB_TOKEN" : null;

        if (!hasEnvToken)
        {
            if (primaryInfo.LoggedIn && primaryInfo.ActiveAccount != false)
            {
                var state = primaryInfo.SourceIsKeyring
                    ? AuthenticationState.AuthenticatedViaKeyring
                    : AuthenticationState.Authenticated;
                return Finalize(state, primaryInfo, null, requiredScopes);
            }
            return NotAuthenticated(primaryInfo);
        }

        bool primaryLooksValid = primary.Success
                                  && primaryInfo.LoggedIn
                                  && primaryInfo.ActiveAccount != false
                                  && !primaryInfo.HasTokenFailure;

        if (primaryLooksValid)
            return Finalize(AuthenticationState.AuthenticatedViaEnvironmentToken, primaryInfo, relevantVariable, requiredScopes);

        // Primary check failed (or reported an explicit invalid-token error).
        // Was there a valid keyring login hiding underneath the bad token?
        var badVariable = primaryInfo.FailedTokenVariable ?? relevantVariable;

        if (cleaned is not null)
        {
            var cleanedInfo = GitHubAuthStatusParser.Parse(cleaned);
            if (cleanedInfo.LoggedIn && cleanedInfo.ActiveAccount != false)
            {
                return Finalize(
                    AuthenticationState.EnvironmentTokenOverridesValidKeyring,
                    cleanedInfo,
                    badVariable,
                    requiredScopes,
                    overrideExplanation:
                        "Ein ungültiger Token in der Windows-Umgebung verhindert die Nutzung Ihrer " +
                        "bereits gültigen GitHub-Anmeldung.\n\nDer Token wird aus der Benutzer-Umgebung entfernt. " +
                        "Die sichere Anmeldung im Windows-Schlüsselspeicher bleibt erhalten.");
            }
        }

        return new AuthenticationDiagnosis(
            AuthenticationState.InvalidEnvironmentToken,
            "Ungültiger Zugangs-Token",
            $"Der Token in {badVariable} ist ungültig, und es ist keine gültige GitHub-Anmeldung vorhanden. " +
            "Bitte einmalig anmelden.",
            primaryInfo.RawOutput,
            null,
            primaryInfo.Scopes,
            Array.Empty<string>(),
            badVariable,
            CanAutoRepair: false);
    }

    public static AuthenticationDiagnosis CliUnavailable() =>
        new(AuthenticationState.GitHubCliUnavailable,
            "GitHub CLI nicht gefunden",
            "Die GitHub CLI (gh) wurde nicht gefunden oder konnte nicht gestartet werden. Bitte installieren.",
            string.Empty,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CanAutoRepair: false);

    public static AuthenticationDiagnosis CheckFailed(string technicalDetails) =>
        new(AuthenticationState.AuthenticationCheckFailed,
            "Prüfung fehlgeschlagen",
            "Die Authentifizierungsprüfung konnte nicht durchgeführt werden.",
            technicalDetails,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CanAutoRepair: false);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AuthenticationDiagnosis Finalize(
        AuthenticationState baseState,
        GitHubAuthStatusInfo info,
        string? relevantVariable,
        IReadOnlyList<string> requiredScopes,
        string? overrideExplanation = null)
    {
        var missing = requiredScopes
            .Where(required => !info.Scopes.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length > 0 && baseState != AuthenticationState.EnvironmentTokenOverridesValidKeyring)
        {
            return new AuthenticationDiagnosis(
                AuthenticationState.MissingRequiredScopes,
                "Berechtigung fehlt",
                $"GitHub-Anmeldung gültig, aber benötigte Berechtigung fehlt: {string.Join(", ", missing)}.",
                info.RawOutput,
                info.Account,
                info.Scopes,
                missing,
                relevantVariable,
                CanAutoRepair: false);
        }

        var (title, explanation) = baseState switch
        {
            AuthenticationState.AuthenticatedViaKeyring => (
                "Angemeldet",
                $"GitHub-Konto {info.Account} über den sicheren Windows-Schlüsselspeicher verbunden."),
            AuthenticationState.AuthenticatedViaEnvironmentToken => (
                "Angemeldet",
                $"GitHub-Konto {info.Account} über einen Zugangs-Token in {relevantVariable} verbunden."),
            AuthenticationState.EnvironmentTokenOverridesValidKeyring => (
                "Anmeldung blockiert",
                overrideExplanation ?? "Ein ungültiger Token überschreibt eine gültige GitHub-Anmeldung."),
            AuthenticationState.Authenticated => (
                "Angemeldet",
                $"GitHub-Konto {info.Account} verbunden."),
            _ => ("Angemeldet", $"GitHub-Konto {info.Account} verbunden.")
        };

        bool canRepair = baseState == AuthenticationState.EnvironmentTokenOverridesValidKeyring;

        return new AuthenticationDiagnosis(
            baseState, title, explanation, info.RawOutput, info.Account, info.Scopes,
            Array.Empty<string>(), relevantVariable, canRepair);
    }

    private static AuthenticationDiagnosis NotAuthenticated(GitHubAuthStatusInfo info) =>
        new(AuthenticationState.NotAuthenticated,
            "Nicht angemeldet",
            "Es wurde keine gültige GitHub-Anmeldung gefunden.",
            info.RawOutput,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CanAutoRepair: false);
}
