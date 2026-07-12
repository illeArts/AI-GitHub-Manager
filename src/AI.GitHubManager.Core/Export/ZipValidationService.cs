using System.IO.Compression;

namespace AI.GitHubManager.Core.Export;

public sealed class ZipValidationService
{
    public Task<ZipValidationResult> ValidateAsync(ExportPlan plan, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        try
        {
            using var archive = ZipFile.OpenRead(plan.DestinationPath);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string safe;
                try { safe = ExportPath.NormalizeRelative(entry.FullName); }
                catch { errors.Add($"Unsicherer Pfad im Archiv: {entry.FullName}"); continue; }
                actual.Add(safe);
                using var stream = entry.Open();
                stream.CopyTo(Stream.Null);
            }
            var expected = plan.Included.Select(x => x.ArchivePath).ToHashSet(StringComparer.Ordinal);
            foreach (var missing in expected.Except(actual)) errors.Add($"Fehlt: {missing}");
            foreach (var unexpected in actual.Except(expected)) errors.Add($"Nicht geplant: {unexpected}");
        }
        catch (Exception ex) { errors.Add($"Archiv kann nicht gelesen werden: {ex.Message}"); }
        return Task.FromResult(new ZipValidationResult(errors.Count == 0, errors));
    }
}
