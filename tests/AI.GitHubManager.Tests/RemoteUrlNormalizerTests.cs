using AI.GitHubManager.Core.Remote;
using Xunit;

namespace AI.GitHubManager.Tests;

public class RemoteUrlNormalizerTests
{
    // 1. Normal HTTPS URL is accepted.
    [Fact]
    public void Parse_HttpsUrl_IsAccepted()
    {
        var info = RemoteUrlNormalizer.Parse("https://github.com/illeArts/AndreAIAgent.git");
        Assert.NotNull(info);
        Assert.Equal("github.com", info!.Host);
        Assert.Equal("illeArts", info.Owner);
        Assert.Equal("AndreAIAgent", info.Repository);
        Assert.False(info.IsSsh);
        Assert.False(info.ContainsCredentials);
    }

    // 2. HTTPS URL without .git is accepted.
    [Fact]
    public void Parse_HttpsUrlWithoutGitSuffix_IsAccepted()
    {
        var info = RemoteUrlNormalizer.Parse("https://github.com/illeArts/AndreAIAgent");
        Assert.NotNull(info);
        Assert.Equal("AndreAIAgent", info!.Repository);
    }

    // 3. SSH URL is accepted (both shorthand and ssh:// form).
    [Fact]
    public void Parse_SshShorthand_IsAccepted()
    {
        var info = RemoteUrlNormalizer.Parse("git@github.com:illeArts/AndreAIAgent.git");
        Assert.NotNull(info);
        Assert.True(info!.IsSsh);
        Assert.Equal("illeArts", info.Owner);
        Assert.Equal("AndreAIAgent", info.Repository);
    }

    [Fact]
    public void Parse_SshUri_IsAccepted()
    {
        var info = RemoteUrlNormalizer.Parse("ssh://git@github.com/illeArts/AndreAIAgent.git");
        Assert.NotNull(info);
        Assert.True(info!.IsSsh);
    }

    // 4. Token in URL is recognised and redacted.
    [Fact]
    public void Parse_CredentialInUrl_IsDetectedAndMasked()
    {
        var info = RemoteUrlNormalizer.Parse("https://ghp_realtokenvalue1234@github.com/illeArts/AndreAIAgent.git");
        Assert.NotNull(info);
        Assert.True(info!.ContainsCredentials);
        Assert.NotNull(info.MaskedCredential);
        Assert.DoesNotContain("realtokenvalue", info.MaskedCredential);
    }

    // 5. DEIN_VORHANDENER_TOKEN is recognised as a broken placeholder.
    [Fact]
    public void ContainsPlaceholderToken_DetectsKnownPlaceholder()
    {
        Assert.True(RemoteUrlNormalizer.ContainsPlaceholderToken(
            "https://DEIN_VORHANDENER_TOKEN@github.com/illeArts/AndreAIAgent.git"));
    }

    [Fact]
    public void ContainsPlaceholderToken_FalseForRealUrl()
    {
        Assert.False(RemoteUrlNormalizer.ContainsPlaceholderToken("https://github.com/illeArts/AndreAIAgent.git"));
    }

    // 6. Safe sanitization sets the canonical URL.
    [Fact]
    public void Sanitize_RemovesCredentialsAndReturnsCanonicalHttpsUrl()
    {
        var sanitized = RemoteUrlNormalizer.Sanitize("https://DEIN_VORHANDENER_TOKEN@github.com/illeArts/AndreAIAgent.git");
        Assert.Equal("https://github.com/illeArts/AndreAIAgent.git", sanitized);
    }

    [Fact]
    public void Sanitize_SshUrl_ReturnsCanonicalSshForm()
    {
        var sanitized = RemoteUrlNormalizer.Sanitize("ssh://git@github.com/illeArts/AndreAIAgent.git");
        Assert.Equal("git@github.com:illeArts/AndreAIAgent.git", sanitized);
    }

    // 7. A genuinely different owner/repository must still trigger a warning (no false negative).
    [Fact]
    public void AreEquivalent_FalseWhenOwnerDiffers()
    {
        Assert.False(RemoteUrlNormalizer.AreEquivalent(
            "https://github.com/illeArts/AndreAIAgent.git",
            "https://github.com/someone-else/AndreAIAgent.git"));
    }

    [Fact]
    public void AreEquivalent_FalseWhenRepositoryDiffers()
    {
        Assert.False(RemoteUrlNormalizer.AreEquivalent(
            "https://github.com/illeArts/AndreAIAgent.git",
            "https://github.com/illeArts/SomeOtherRepo.git"));
    }

    [Theory]
    [InlineData("https://github.com/illeArts/AndreAIAgent.git", "https://github.com/illeArts/AndreAIAgent")]
    [InlineData("https://github.com/illeArts/AndreAIAgent.git", "git@github.com:illeArts/AndreAIAgent.git")]
    [InlineData("https://github.com/illeArts/AndreAIAgent.git", "ssh://git@github.com/illeArts/AndreAIAgent.git")]
    [InlineData("https://github.com/illeArts/AndreAIAgent.git", "https://GITHUB.COM/ILLEARTS/ANDREAIAGENT.GIT")]
    public void AreEquivalent_TrueForAllAcceptedNotations(string a, string b)
    {
        Assert.True(RemoteUrlNormalizer.AreEquivalent(a, b));
    }
}
