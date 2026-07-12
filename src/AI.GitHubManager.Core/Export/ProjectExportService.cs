using System.IO.Compression;

namespace AI.GitHubManager.Core.Export;

public sealed class ProjectExportService
{
    public async Task<ExportResult> ExportAsync(ExportPlan plan, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(plan.DestinationPath)) throw new IOException("Das Zielarchiv existiert bereits.");
        Directory.CreateDirectory(Path.GetDirectoryName(plan.DestinationPath)!);
        long written = 0;
        var included = plan.Included;
        try
        {
            await using var output = new FileStream(plan.DestinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false);
            for (var i = 0; i < included.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = included[i];
                var info = new FileInfo(item.SourcePath);
                if (!info.Exists || info.Length != item.Length || info.LastWriteTimeUtc != item.LastWriteTimeUtc)
                    throw new IOException($"Quelldatei wurde nach der Vorschau verändert: {item.RelativePath}");
                var entry = archive.CreateEntry(item.ArchivePath, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(item.LastWriteTimeUtc);
                await using var source = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await using var target = entry.Open();
                await source.CopyToAsync(target, cancellationToken);
                written += item.Length;
                progress?.Report(new ExportProgress(i + 1, included.Count, item.RelativePath, written));
            }
            return new ExportResult(true, plan.DestinationPath, included.Count, written);
        }
        catch
        {
            try { if (File.Exists(plan.DestinationPath)) File.Delete(plan.DestinationPath); } catch { }
            throw;
        }
    }
}
