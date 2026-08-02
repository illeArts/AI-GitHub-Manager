using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Operations;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Covers Teil B7/C: dangerous or advanced operation selections must never
/// be persisted as the automatic default for next launch, while normal
/// (Safe/Caution) selections should be, and loading must be fault-tolerant
/// for missing/unknown stored ids.
/// </summary>
public class AppSettingsServiceOperationPersistenceTests
{
    [Fact]
    public void NewSettings_DefaultSelectedOperation_IsUpdate()
    {
        var settings = new AppSettingsService();
        Assert.Same(GitOperationCatalog.Update, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void SetSelectedOperationIfSafe_SafeOperation_IsStoredAndReturned()
    {
        var settings = new AppSettingsService();

        var stored = settings.SetSelectedOperationIfSafe(GitOperationCatalog.Status);

        Assert.True(stored);
        Assert.Equal("status", settings.SelectedOperationId);
        Assert.Same(GitOperationCatalog.Status, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void SetSelectedOperationIfSafe_CautionOperation_IsStoredAndReturned()
    {
        var settings = new AppSettingsService();

        var stored = settings.SetSelectedOperationIfSafe(GitOperationCatalog.CommitAndUpload);

        Assert.True(stored);
        Assert.Same(GitOperationCatalog.CommitAndUpload, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void SetSelectedOperationIfSafe_AdvancedOperation_IsNeverStored()
    {
        var settings = new AppSettingsService();
        settings.SetSelectedOperationIfSafe(GitOperationCatalog.Status); // establish a known-good prior selection

        var stored = settings.SetSelectedOperationIfSafe(GitOperationCatalog.Rebase);

        Assert.False(stored);
        // The previous safe selection must be untouched — Rebase must never overwrite it.
        Assert.Equal("status", settings.SelectedOperationId);
        Assert.Same(GitOperationCatalog.Status, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void SetSelectedOperationIfSafe_DangerousOperation_IsNeverStored()
    {
        var settings = new AppSettingsService();

        var stored = settings.SetSelectedOperationIfSafe(GitOperationCatalog.HardReset);

        Assert.False(stored);
        Assert.NotEqual("hard-reset", settings.SelectedOperationId);
        // Falls back to the safe default, never to the dangerous one.
        Assert.Same(GitOperationCatalog.Update, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void GetSelectedOperationOrDefault_UnknownStoredId_FallsBackToUpdate()
    {
        var settings = new AppSettingsService { SelectedOperationId = "some-id-from-a-future-version" };
        Assert.Same(GitOperationCatalog.Update, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void GetSelectedOperationOrDefault_EmptyOrNullStoredId_FallsBackToUpdate()
    {
        var settings = new AppSettingsService { SelectedOperationId = "" };
        Assert.Same(GitOperationCatalog.Update, settings.GetSelectedOperationOrDefault());
    }

    [Fact]
    public void GetSelectedOperationOrDefault_StoredDangerousIdDirectlyOnTheField_IsRejectedDefensively()
    {
        // Simulates a hand-edited or corrupted settings file that somehow
        // contains a dangerous id directly (bypassing SetSelectedOperationIfSafe).
        // GetSelectedOperationOrDefault must defend against this too.
        var settings = new AppSettingsService { SelectedOperationId = "hard-reset" };
        Assert.Same(GitOperationCatalog.Update, settings.GetSelectedOperationOrDefault());
    }
}
