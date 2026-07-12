namespace AI.GitHubManager.Core.Export;

public static class SensitiveFileScanner
{
    public static IReadOnlyList<string> Patterns { get; } =
        [".env", ".env.*", "*.pem", "*.key", "*.p12", "*.pfx", "*.mobileprovision", "id_rsa", "id_ed25519", "secrets.json", "appsettings.Development.json"];

    public static SensitiveFileFinding? Find(string relativePath)
    {
        var name = Path.GetFileName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var pattern in Patterns)
        {
            var matched = pattern.StartsWith("*.", StringComparison.Ordinal)
                ? name.EndsWith(pattern[1..], comparison)
                : pattern == ".env.*"
                    ? name.StartsWith(".env.", comparison)
                    : name.Equals(pattern, comparison);
            if (matched) return new SensitiveFileFinding(relativePath, pattern);
        }
        return null;
    }
}
