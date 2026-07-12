using AI.GitHubManager.Core.Export;
using Xunit;

namespace AI.GitHubManager.Tests;

public class ExportRuleTests
{
    [Theory]
    [InlineData("src/bin/app.dll", "**/bin/**", true)]
    [InlineData("src/main.cs", "**/bin/**", false)]
    [InlineData(".git/config", ".git/**", true)]
    [InlineData(".gitignore", ".git/**", false)]
    [InlineData("folder/file.tmp", "**/*.tmp", true)]
    public void MatchesGlobPatterns(string path, string pattern, bool expected) => Assert.Equal(expected, ExportRuleMatcher.IsMatch(path, pattern));

    [Fact] public void NormalizesWindowsSeparators() => Assert.Equal("src/file.cs", ExportPath.NormalizeRelative("src\\file.cs"));
    [Theory, InlineData("../secret"), InlineData("a/../secret"), InlineData("/absolute")]
    public void RejectsUnsafePaths(string path) => Assert.Throws<ArgumentException>(() => ExportPath.NormalizeRelative(path));

    [Theory]
    [InlineData(".env")]
    [InlineData("server/.env.local")]
    [InlineData("keys/client.p12")]
    [InlineData("config/secrets.json")]
    [InlineData("id_ed25519")]
    public void FindsSensitiveNames(string path) => Assert.NotNull(SensitiveFileScanner.Find(path));

    [Fact] public void DoesNotFlagNormalConfig() => Assert.Null(SensitiveFileScanner.Find("appsettings.json"));
}
