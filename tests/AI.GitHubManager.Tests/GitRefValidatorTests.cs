using AI.GitHubManager.Core.Git;
using Xunit;

namespace AI.GitHubManager.Tests;

public class GitRefValidatorTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("feature/x")]
    [InlineData("a1b2c3d")]
    [InlineData("HEAD~1")]
    public void IsValidRef_AcceptsNormalRefs(string value) => Assert.True(GitRefValidator.IsValidRef(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-D")]
    [InlineData("--upload-pack=evil")]
    [InlineData("main branch")]
    [InlineData("a..b")]
    public void IsValidRef_RejectsUnsafeOrEmptyInput(string? value) => Assert.False(GitRefValidator.IsValidRef(value));

    [Fact]
    public void ValidationError_ReturnsNullForValidRef() => Assert.Null(GitRefValidator.ValidationError("main"));

    [Fact]
    public void ValidationError_ReturnsBilingualMessageForInvalidRef()
    {
        var error = GitRefValidator.ValidationError("-D");
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.Value.De));
        Assert.False(string.IsNullOrWhiteSpace(error!.Value.En));
        Assert.NotEqual(error!.Value.De, error!.Value.En);
    }
}
