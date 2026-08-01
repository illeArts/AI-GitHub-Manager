using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Process;
using Xunit;

namespace AI.GitHubManager.Tests;

public class AuthenticationDiagnosisResolverTests
{
    private static readonly string[] RequiredScopes = ["repo", "read:org"];
    private static readonly string[] WorkflowScopes = ["repo", "read:org", "workflow"];

    private static CommandResult Ok(string output) => new(0, output, string.Empty, "gh", "auth status");
    private static CommandResult Fail(string output) => new(1, string.Empty, output, "gh", "auth status");

    private const string KeyringLoginOutput =
        "✓ Logged in to github.com account illeArts (keyring)\n" +
        "- Active account: true\n" +
        "- Token scopes: gist, read:org, repo, workflow";

    private const string KeyringLoginInactiveOutput =
        "✓ Logged in to github.com account illeArts (keyring)\n" +
        "- Active account: false\n" +
        "- Token scopes: gist, read:org, repo, workflow";

    private static string EnvTokenLoginOutput(string variable) =>
        $"✓ Logged in to github.com account illeArts ({variable})\n" +
        "- Active account: true\n" +
        "- Token scopes: gist, read:org, repo, workflow";

    private static string InvalidTokenOutput(string variable) =>
        $"X Failed to log in to github.com using token ({variable})\n" +
        $"- The token in {variable} is invalid.";

    // 1. No token, valid keyring → AuthenticatedViaKeyring
    [Fact]
    public void NoToken_ValidKeyring_ReturnsAuthenticatedViaKeyring()
    {
        var result = AuthenticationDiagnosisResolver.Resolve(
            Ok(KeyringLoginOutput), cleaned: null, ghTokenEnv: null, githubTokenEnv: null, RequiredScopes);

        Assert.Equal(AuthenticationState.AuthenticatedViaKeyring, result.State);
        Assert.Equal("illeArts", result.ActiveAccount);
        Assert.False(result.CanAutoRepair);
    }

    // 2. Valid GITHUB_TOKEN → AuthenticatedViaEnvironmentToken
    [Fact]
    public void ValidGithubToken_ReturnsAuthenticatedViaEnvironmentToken()
    {
        var result = AuthenticationDiagnosisResolver.Resolve(
            Ok(EnvTokenLoginOutput("GITHUB_TOKEN")), cleaned: null,
            ghTokenEnv: null, githubTokenEnv: "ghp_validvalidvalidvalid", RequiredScopes);

        Assert.Equal(AuthenticationState.AuthenticatedViaEnvironmentToken, result.State);
        Assert.Equal("GITHUB_TOKEN", result.RelevantEnvironmentVariable);
    }

    // 3. Invalid GITHUB_TOKEN, no keyring → InvalidEnvironmentToken
    [Fact]
    public void InvalidGithubToken_NoKeyring_ReturnsInvalidEnvironmentToken()
    {
        var primary = Fail(InvalidTokenOutput("GITHUB_TOKEN"));
        var cleaned = Fail("You are not logged in to any GitHub hosts.");

        var result = AuthenticationDiagnosisResolver.Resolve(
            primary, cleaned, ghTokenEnv: null, githubTokenEnv: "ghp_invalidinvalidinvalid", RequiredScopes);

        Assert.Equal(AuthenticationState.InvalidEnvironmentToken, result.State);
        Assert.Equal("GITHUB_TOKEN", result.RelevantEnvironmentVariable);
        Assert.False(result.CanAutoRepair);
    }

    // 4. Invalid GITHUB_TOKEN, valid keyring → EnvironmentTokenOverridesValidKeyring (the reference bug).
    // Primary output matches the real-world report exactly: gh still lists the keyring account
    // but marks it inactive because the (invalid) env token takes precedence.
    [Fact]
    public void InvalidGithubToken_ValidKeyring_ReturnsEnvironmentTokenOverridesValidKeyring()
    {
        var primary = Fail(InvalidTokenOutput("GITHUB_TOKEN") + "\n\n" + KeyringLoginInactiveOutput);
        var cleaned = Ok(KeyringLoginOutput); // after removing the token, the keyring account becomes active

        var result = AuthenticationDiagnosisResolver.Resolve(
            primary, cleaned, ghTokenEnv: null, githubTokenEnv: "ghp_invalidinvalidinvalid", RequiredScopes);

        Assert.Equal(AuthenticationState.EnvironmentTokenOverridesValidKeyring, result.State);
        Assert.Equal("GITHUB_TOKEN", result.RelevantEnvironmentVariable);
        Assert.True(result.CanAutoRepair);
        Assert.Contains("bereits gültigen GitHub-Anmeldung", result.Explanation);
    }

    // 5. Invalid GH_TOKEN, valid keyring → EnvironmentTokenOverridesValidKeyring
    [Fact]
    public void InvalidGhToken_ValidKeyring_ReturnsEnvironmentTokenOverridesValidKeyring()
    {
        var primary = Fail(InvalidTokenOutput("GH_TOKEN"));
        var cleaned = Ok(KeyringLoginOutput);

        var result = AuthenticationDiagnosisResolver.Resolve(
            primary, cleaned, ghTokenEnv: "ghp_invalidinvalidinvalid", githubTokenEnv: null, RequiredScopes);

        Assert.Equal(AuthenticationState.EnvironmentTokenOverridesValidKeyring, result.State);
        Assert.Equal("GH_TOKEN", result.RelevantEnvironmentVariable);
    }

    // 6. Both variables set, GH_TOKEN takes priority (matches real `gh` behaviour) —
    //    here GH_TOKEN is invalid and GITHUB_TOKEN would have been valid, but gh only
    //    ever tries GH_TOKEN, so the diagnosis must name GH_TOKEN as the culprit.
    [Fact]
    public void BothTokensSet_GhTokenInvalid_PriorityDeterminesResult()
    {
        var primary = Fail(InvalidTokenOutput("GH_TOKEN"));
        var cleaned = Fail("You are not logged in to any GitHub hosts.");

        var result = AuthenticationDiagnosisResolver.Resolve(
            primary, cleaned,
            ghTokenEnv: "ghp_invalidinvalidinvalid",
            githubTokenEnv: "ghp_wouldbevalidwouldbevalid",
            RequiredScopes);

        Assert.Equal(AuthenticationState.InvalidEnvironmentToken, result.State);
        Assert.Equal("GH_TOKEN", result.RelevantEnvironmentVariable);
    }

    // 7. Keyring valid, but "workflow" scope missing → MissingRequiredScopes
    [Fact]
    public void ValidKeyring_MissingWorkflowScope_ReturnsMissingRequiredScopes()
    {
        const string output =
            "✓ Logged in to github.com account illeArts (keyring)\n" +
            "- Active account: true\n" +
            "- Token scopes: gist, read:org, repo";

        var result = AuthenticationDiagnosisResolver.Resolve(
            Ok(output), cleaned: null, ghTokenEnv: null, githubTokenEnv: null, WorkflowScopes);

        Assert.Equal(AuthenticationState.MissingRequiredScopes, result.State);
        Assert.Contains("workflow", result.MissingScopes);
    }

    // 8. gh CLI not installed → understandable diagnosis, no crash
    [Fact]
    public void CliUnavailable_ReturnsUnderstandableDiagnosisWithoutThrowing()
    {
        var result = AuthenticationDiagnosisResolver.CliUnavailable();

        Assert.Equal(AuthenticationState.GitHubCliUnavailable, result.State);
        Assert.False(result.IsUsable);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
    }

    [Fact]
    public void NotLoggedIn_NoToken_ReturnsNotAuthenticated()
    {
        var result = AuthenticationDiagnosisResolver.Resolve(
            Fail("You are not logged in to any GitHub hosts."),
            cleaned: null, ghTokenEnv: null, githubTokenEnv: null, RequiredScopes);

        Assert.Equal(AuthenticationState.NotAuthenticated, result.State);
    }

    [Fact]
    public void ParsedOutput_NeverContainsRawTokenValue()
    {
        var primary = Fail(InvalidTokenOutput("GITHUB_TOKEN") + "\nSuspicious raw value: ghp_abcdefghijklmnopqrstuvwxyz1234");
        var result = AuthenticationDiagnosisResolver.Resolve(
            primary, cleaned: null, ghTokenEnv: null, githubTokenEnv: "ghp_abcdefghijklmnopqrstuvwxyz1234", RequiredScopes);

        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz1234", result.TechnicalDetails);
    }
}
