using AI.GitHubManager.Core.Operations;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Covers Teil B6: the "Was passiert jetzt?"/"Zusammenfassung" preview shown
/// under the dropdown must exist for the operations that are actually
/// executable in this milestone, must be genuinely bilingual, and must not
/// silently invent a preview for operations that don't have one yet.
/// </summary>
public class OperationPreflightPreviewTests
{
    [Theory]
    [InlineData("update")]
    [InlineData("status")]
    [InlineData("commit-and-upload")]
    public void Steps_ExecutableOperations_ReturnNonEmptyBilingualSteps(string id)
    {
        var op = GitOperationCatalog.Find(id);
        Assert.NotNull(op);

        var stepsDe = OperationPreflightPreview.Steps(op!, english: false);
        var stepsEn = OperationPreflightPreview.Steps(op!, english: true);

        Assert.NotEmpty(stepsDe);
        Assert.NotEmpty(stepsEn);
        Assert.Equal(stepsDe.Count, stepsEn.Count);
        Assert.NotEqual(stepsDe[0], stepsEn[0]);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("status")]
    [InlineData("commit-and-upload")]
    public void Summary_ExecutableOperations_IsNonEmptyBilingual(string id)
    {
        var op = GitOperationCatalog.Find(id);
        Assert.NotNull(op);

        var summaryDe = OperationPreflightPreview.Summary(op!, english: false);
        var summaryEn = OperationPreflightPreview.Summary(op!, english: true);

        Assert.False(string.IsNullOrWhiteSpace(summaryDe));
        Assert.False(string.IsNullOrWhiteSpace(summaryEn));
        Assert.NotEqual(summaryDe, summaryEn);
    }

    [Fact]
    public void Steps_OperationWithoutCannedPreview_ReturnsEmpty_NotFabricated()
    {
        var op = GitOperationCatalog.Find("diff");
        Assert.NotNull(op);
        Assert.Empty(OperationPreflightPreview.Steps(op!, english: false));
        Assert.Null(OperationPreflightPreview.Summary(op!, english: false));
    }

    [Fact]
    public void Steps_UpdateOperation_MatchesConcreteExampleRequirement()
    {
        // Teil B6 explicitly requires a concrete example for "Aktualisieren".
        var update = GitOperationCatalog.Update;
        var steps = OperationPreflightPreview.Steps(update, english: false);

        Assert.Contains(steps, s => s.Contains("fetch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, s => s.Contains("pull", StringComparison.OrdinalIgnoreCase));
    }
}
