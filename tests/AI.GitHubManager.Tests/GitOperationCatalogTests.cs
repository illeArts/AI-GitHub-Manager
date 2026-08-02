using AI.GitHubManager.Core.Operations;
using Xunit;

namespace AI.GitHubManager.Tests;

public class GitOperationCatalogTests
{
    // ── Default selection (Teil B1/C: "Aktualisieren") ──────────────────────

    [Fact]
    public void DefaultOperationId_IsUpdate()
    {
        Assert.Equal("update", GitOperationCatalog.DefaultOperationId);
        Assert.Same(GitOperationCatalog.Update, GitOperationCatalog.Find(GitOperationCatalog.DefaultOperationId));
    }

    [Fact]
    public void Update_IsInNormalOperations_AsExecutable()
    {
        Assert.Contains(GitOperationCatalog.Update, GitOperationCatalog.NormalOperations);
        Assert.True(GitOperationCatalog.Update.IsExecutable);
    }

    // ── No duplicate/misleading entries (explicit user requirement) ─────────

    [Fact]
    public void NormalOperations_HasNoDuplicateIds()
    {
        var ids = GitOperationCatalog.NormalOperations.Select(o => o.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AllOperations_HasNoDuplicateIdsAcrossNormalAndAdvanced()
    {
        var ids = GitOperationCatalog.All.Select(o => o.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void CheckForChanges_And_DownloadChanges_AreMergedIntoOneEntry()
    {
        // The brief explicitly warns against "Änderungen prüfen" and
        // "Änderungen herunterladen" ending up as two separate, technically
        // identical entries. Both map to `git fetch` with zero working-tree
        // impact, so there must be exactly one normal operation using
        // git fetch as its technical command, not two.
        var fetchOperations = GitOperationCatalog.NormalOperations
            .Where(o => o.TechnicalCommand == "git fetch")
            .ToList();

        Assert.Single(fetchOperations);
        Assert.Equal("check-for-changes", fetchOperations[0].Id);
        Assert.Equal(12, GitOperationCatalog.NormalOperations.Count);
    }

    // ── Normal vs. advanced separation (Teil B1/B2) ──────────────────────────

    [Fact]
    public void NormalOperations_NeverContainAdvancedCommands()
    {
        string[] advancedIds = { "rebase", "cherry-pick", "force-push", "reset", "hard-reset", "clean" };
        foreach (var id in advancedIds)
            Assert.DoesNotContain(GitOperationCatalog.NormalOperations, o => o.Id == id);
    }

    [Fact]
    public void AdvancedOperations_AreAllFlaggedIsAdvanced()
    {
        Assert.All(GitOperationCatalog.AdvancedOperations, o => Assert.True(o.IsAdvanced));
    }

    [Fact]
    public void NormalOperations_AreNeverFlaggedIsAdvanced()
    {
        Assert.All(GitOperationCatalog.NormalOperations, o => Assert.False(o.IsAdvanced));
    }

    [Fact]
    public void AdvancedOperations_ContainsExactlyTheSixNamedInTheBrief()
    {
        var ids = GitOperationCatalog.AdvancedOperations.Select(o => o.Id).OrderBy(x => x).ToArray();
        Assert.Equal(
            new[] { "cherry-pick", "clean", "force-push", "hard-reset", "rebase", "reset" },
            ids);
    }

    // ── Risk levels (Teil B5) ─────────────────────────────────────────────────

    [Theory]
    [InlineData("check-for-changes", GitOperationRiskLevel.Safe)]
    [InlineData("status", GitOperationRiskLevel.Safe)]
    [InlineData("diff", GitOperationRiskLevel.Safe)]
    [InlineData("commit", GitOperationRiskLevel.Safe)]
    [InlineData("update", GitOperationRiskLevel.Caution)]
    [InlineData("upload-changes", GitOperationRiskLevel.Caution)]
    [InlineData("switch-branch", GitOperationRiskLevel.Caution)]
    [InlineData("merge", GitOperationRiskLevel.Caution)]
    [InlineData("stash-restore", GitOperationRiskLevel.Caution)]
    [InlineData("commit-and-upload", GitOperationRiskLevel.Caution)]
    public void NormalOperations_HaveExpectedRiskLevel(string id, GitOperationRiskLevel expected)
    {
        var op = GitOperationCatalog.Find(id);
        Assert.NotNull(op);
        Assert.Equal(expected, op!.RiskLevel);
    }

    [Theory]
    [InlineData("rebase", GitOperationRiskLevel.Advanced)]
    [InlineData("cherry-pick", GitOperationRiskLevel.Advanced)]
    [InlineData("reset", GitOperationRiskLevel.Advanced)]
    [InlineData("force-push", GitOperationRiskLevel.Dangerous)]
    [InlineData("hard-reset", GitOperationRiskLevel.Dangerous)]
    [InlineData("clean", GitOperationRiskLevel.Dangerous)]
    public void AdvancedOperations_HaveExpectedRiskLevel(string id, GitOperationRiskLevel expected)
    {
        var op = GitOperationCatalog.Find(id);
        Assert.NotNull(op);
        Assert.Equal(expected, op!.RiskLevel);
    }

    // ── Safe-to-persist rule (Teil B7/C) ──────────────────────────────────────

    [Fact]
    public void IsSafeToPersistAsDefault_TrueForSafeAndCaution()
    {
        Assert.True(GitOperationCatalog.IsSafeToPersistAsDefault(GitOperationCatalog.Update));   // Caution
        Assert.True(GitOperationCatalog.IsSafeToPersistAsDefault(GitOperationCatalog.Status));   // Safe
    }

    [Fact]
    public void IsSafeToPersistAsDefault_FalseForAdvancedAndDangerous()
    {
        Assert.False(GitOperationCatalog.IsSafeToPersistAsDefault(GitOperationCatalog.Rebase));      // Advanced
        Assert.False(GitOperationCatalog.IsSafeToPersistAsDefault(GitOperationCatalog.ForcePush));   // Dangerous
        Assert.False(GitOperationCatalog.IsSafeToPersistAsDefault(GitOperationCatalog.HardReset));   // Dangerous
        Assert.False(GitOperationCatalog.IsSafeToPersistAsDefault(GitOperationCatalog.Clean));       // Dangerous
    }

    // ── Bilingual text completeness (Teil A1: language switch must cover everything) ──

    [Fact]
    public void AllOperations_HaveNonEmptyBilingualText()
    {
        foreach (var op in GitOperationCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(op.TitleDe), $"{op.Id}: TitleDe empty");
            Assert.False(string.IsNullOrWhiteSpace(op.TitleEn), $"{op.Id}: TitleEn empty");
            Assert.False(string.IsNullOrWhiteSpace(op.DescriptionDe), $"{op.Id}: DescriptionDe empty");
            Assert.False(string.IsNullOrWhiteSpace(op.DescriptionEn), $"{op.Id}: DescriptionEn empty");
            Assert.False(string.IsNullOrWhiteSpace(op.SuitableForDe), $"{op.Id}: SuitableForDe empty");
            Assert.False(string.IsNullOrWhiteSpace(op.SuitableForEn), $"{op.Id}: SuitableForEn empty");
            Assert.False(string.IsNullOrWhiteSpace(op.WhatChangesDe), $"{op.Id}: WhatChangesDe empty");
            Assert.False(string.IsNullOrWhiteSpace(op.WhatChangesEn), $"{op.Id}: WhatChangesEn empty");
            Assert.False(string.IsNullOrWhiteSpace(op.WhatStaysDe), $"{op.Id}: WhatStaysDe empty");
            Assert.False(string.IsNullOrWhiteSpace(op.WhatStaysEn), $"{op.Id}: WhatStaysEn empty");
            Assert.False(string.IsNullOrWhiteSpace(op.RiskDe), $"{op.Id}: RiskDe empty");
            Assert.False(string.IsNullOrWhiteSpace(op.RiskEn), $"{op.Id}: RiskEn empty");
            Assert.False(string.IsNullOrWhiteSpace(op.TechnicalCommand), $"{op.Id}: TechnicalCommand empty");
        }
    }

    [Fact]
    public void Title_SelectsCorrectLanguage()
    {
        Assert.Equal(GitOperationCatalog.Update.TitleDe, GitOperationCatalog.Update.Title(english: false));
        Assert.Equal(GitOperationCatalog.Update.TitleEn, GitOperationCatalog.Update.Title(english: true));
    }

    // ── Find() lookup ──────────────────────────────────────────────────────────

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        Assert.Null(GitOperationCatalog.Find("does-not-exist"));
        Assert.Null(GitOperationCatalog.Find(null));
        Assert.Null(GitOperationCatalog.Find(""));
    }

    [Fact]
    public void Find_KnownId_ReturnsSameInstanceAsCatalogField()
    {
        Assert.Same(GitOperationCatalog.CommitAndUpload, GitOperationCatalog.Find("commit-and-upload"));
        Assert.Same(GitOperationCatalog.HardReset, GitOperationCatalog.Find("hard-reset"));
    }
}
