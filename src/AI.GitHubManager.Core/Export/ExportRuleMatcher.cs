using System.Text.RegularExpressions;

namespace AI.GitHubManager.Core.Export;

public static class ExportRuleMatcher
{
    public static bool IsMatch(string relativePath, string pattern)
    {
        var path = ExportPath.NormalizeRelative(relativePath);
        var p = pattern.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(p) || p.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(p)) return false;
        var regex = "^" + Regex.Escape(p)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]") + "$";
        return Regex.IsMatch(path, regex, OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None);
    }
}
