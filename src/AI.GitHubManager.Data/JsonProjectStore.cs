using System.Text.Json;
using AI.GitHubManager.Core.Projects;

namespace AI.GitHubManager.Data;

public sealed class JsonProjectStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public JsonProjectStore(string? filePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AI.GitHubManager");
        Directory.CreateDirectory(dir);
        _filePath = filePath ?? Path.Combine(dir, "projects.json");
    }

    public async Task<List<ManagedProject>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new List<ManagedProject>();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<ManagedProject>>(json, _options) ?? new List<ManagedProject>();
        }
        catch (JsonException)
        {
            return new List<ManagedProject>();
        }
        catch (IOException)
        {
            return new List<ManagedProject>();
        }
    }

    public async Task SaveAsync(IEnumerable<ManagedProject> projects)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(projects, _options);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
