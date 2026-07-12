using System.IO.Compression;
using AI.GitHubManager.Core.Export;
using Xunit;

namespace AI.GitHubManager.Tests;

public sealed class ProjectExportIntegrationTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "aigm-export-" + Guid.NewGuid().ToString("N"));
    public ProjectExportIntegrationTests() => Directory.CreateDirectory(_temp);

    [Fact]
    public async Task PlanExportAndValidationUseSameEntries()
    {
        var source = CreateSource(); var zip = Path.Combine(_temp, "out.zip");
        var plan = await new ExportPlanService().CreateAsync(source, zip, ExportProfiles.Get(ExportProfileKind.CleanSource));
        Assert.Contains(plan.Excluded, x => x.RelativePath == ".git/config");
        Assert.Contains(plan.Included, x => x.RelativePath == ".gitignore");
        await new ProjectExportService().ExportAsync(plan);
        var validation = await new ZipValidationService().ValidateAsync(plan);
        Assert.True(validation.Success, string.Join("; ", validation.Errors));
        using var archive = ZipFile.OpenRead(zip);
        Assert.Equal(plan.Included.Select(x => x.ArchivePath).Order(), archive.Entries.Select(x => x.FullName).Order());
    }

    [Fact]
    public async Task SensitiveFileCanBeExcludedIndividually()
    {
        var source = CreateSource(); var zip = Path.Combine(_temp, "sensitive.zip");
        var plan = await new ExportPlanService().CreateAsync(source, zip, ExportProfiles.Get(ExportProfileKind.WindowsExchange), sensitiveFilesToExclude: new HashSet<string> { ".env" });
        Assert.False(plan.Entries.Single(x => x.RelativePath == ".env").IsIncluded);
        await new ProjectExportService().ExportAsync(plan);
        Assert.True((await new ZipValidationService().ValidateAsync(plan)).Success);
    }

    [Fact]
    public async Task ExistingArchiveIsNeverOverwritten()
    {
        var source = CreateSource(); var zip = Path.Combine(_temp, "exists.zip"); await File.WriteAllTextAsync(zip, "keep");
        await Assert.ThrowsAsync<IOException>(() => new ExportPlanService().CreateAsync(source, zip, ExportProfiles.Get(ExportProfileKind.WindowsExchange)));
        Assert.Equal("keep", await File.ReadAllTextAsync(zip));
    }

    [Fact]
    public async Task CancellationDeletesPartialArchive()
    {
        var source = CreateSource(); var zip = Path.Combine(_temp, "cancel.zip");
        var plan = await new ExportPlanService().CreateAsync(source, zip, ExportProfiles.Get(ExportProfileKind.WindowsExchange));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProjectExportService().ExportAsync(plan, cancellationToken: cts.Token));
        Assert.False(File.Exists(zip));
    }

    [Fact]
    public void DetectsWorktreePointerWithoutTreatingItAsBackup()
    {
        var main = Path.Combine(_temp, "main"); var wt = Path.Combine(_temp, "worktree"); var gitDir = Path.Combine(main, ".git", "worktrees", "worktree");
        Directory.CreateDirectory(gitDir); Directory.CreateDirectory(wt);
        File.WriteAllText(Path.Combine(wt, ".git"), $"gitdir: {gitDir}");
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/feature/test");
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..");
        var info = new WorktreeDetector().Detect(wt);
        Assert.True(info.IsWorktree); Assert.Equal("feature/test", info.Branch); Assert.Contains("kein eigenständiges", info.Message);
    }

    private string CreateSource()
    {
        var source = Path.Combine(_temp, "project"); Directory.CreateDirectory(Path.Combine(source, ".git")); Directory.CreateDirectory(Path.Combine(source, "src"));
        File.WriteAllText(Path.Combine(source, ".git", "config"), "git"); File.WriteAllText(Path.Combine(source, ".gitignore"), "bin/");
        File.WriteAllText(Path.Combine(source, ".env"), "SECRET=x"); File.WriteAllText(Path.Combine(source, "src", "main.cs"), "class C {}"); return source;
    }
    public void Dispose() { try { Directory.Delete(_temp, true); } catch { } }
}
