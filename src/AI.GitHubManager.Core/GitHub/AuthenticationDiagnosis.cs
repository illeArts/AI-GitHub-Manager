namespace AI.GitHubManager.Core.GitHub;

/// <summary>
/// Result of a full authentication diagnosis: not just "ok/not ok", but the
/// specific reason, in plain language, plus enough technical detail for the
/// "technical mode" view and the diagnostics export.
/// </summary>
public sealed record AuthenticationDiagnosis(
    AuthenticationState State,
    string Title,
    string Explanation,
    string TechnicalDetails,
    string? ActiveAccount,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> MissingScopes,
    string? RelevantEnvironmentVariable,
    bool CanAutoRepair)
{
    /// <summary>True when this state represents a usable, working GitHub login.</summary>
    public bool IsUsable => State is AuthenticationState.Authenticated
                                   or AuthenticationState.AuthenticatedViaKeyring
                                   or AuthenticationState.AuthenticatedViaEnvironmentToken;

    /// <summary>Short user-facing summary line, e.g. for the preflight/push-blocking UI.</summary>
    public string Summary => State switch
    {
        AuthenticationState.EnvironmentTokenOverridesValidKeyring =>
            $"⚠️ Ein ungültiger {RelevantEnvironmentVariable} überschreibt eine gültige GitHub-Anmeldung.",
        AuthenticationState.InvalidEnvironmentToken =>
            $"❌ Der Token in {RelevantEnvironmentVariable} ist ungültig. Keine gültige GitHub-Anmeldung gefunden.",
        AuthenticationState.MissingRequiredScopes =>
            $"⚠️ GitHub-Anmeldung gültig, aber benötigte Berechtigung fehlt: {string.Join(", ", MissingScopes)}.",
        AuthenticationState.NotAuthenticated =>
            "❌ Nicht bei GitHub eingeloggt.",
        AuthenticationState.GitHubCliUnavailable =>
            "❌ GitHub CLI (gh) nicht gefunden.",
        AuthenticationState.AuthenticationCheckFailed =>
            "❌ Authentifizierungsprüfung fehlgeschlagen.",
        AuthenticationState.AuthenticatedViaKeyring =>
            $"✅ GitHub-Konto {ActiveAccount} über sicheren Keyring verbunden.",
        AuthenticationState.AuthenticatedViaEnvironmentToken =>
            $"✅ GitHub-Konto {ActiveAccount} über {RelevantEnvironmentVariable} verbunden.",
        _ => $"✅ GitHub-Konto {ActiveAccount} verbunden."
    };
}
