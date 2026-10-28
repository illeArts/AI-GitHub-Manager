using System.Text.Json;

namespace AI.GitHubManager.Core.Projects;

/// <summary>Add Import.[language].json to extend the catalog; missing keys fall back to English.</summary>
public static class ImportText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = Load();
    public static IReadOnlyCollection<string> Languages => Catalogs.Keys;
    public static IReadOnlyDictionary<string, string> Catalog(string language) => Catalogs[language];
    public static string Get(string key, string language)
        => Catalogs.TryGetValue(language, out var catalog) && catalog.TryGetValue(key, out var text)
            ? text : Catalogs["en"][key];

    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        var assembly = typeof(ImportText).Assembly;
        const string prefix = "AI.GitHubManager.Core.Projects.Resources.Import.";
        return assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix) && n.EndsWith(".json"))
            .ToDictionary(n => n[prefix.Length..^5], n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            });
    }
}
