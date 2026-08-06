using System.Text.Json;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Data;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Covers backward compatibility of projects.json across the new GitHub-link
/// fields, and confirms "Aus Manager entfernen" only ever removes the JSON
/// entry — it must never touch the local folder or any file inside it.
/// </summary>
public sealed class ProjectGitHubLinkPersistenceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();
    private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "ai-github-manager-removal-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_OldJsonWithoutNewFields_LoadsWithSafeDefaults()
    {
        // Simulates a projects.json written by a version of the app that
        // predates RepositoryWebUrl/RepositoryOwner/RepositoryName/RemoteSource.
        var oldJson = """
        [
          {
            "Id": "abc123",
            "Name": "bullbear",
            "Owner": "illeArts-Finance",
            "RemoteUrl": "https://github.com/illeArts-Finance/bullbear.git",
            "DefaultBranch": "main",
            "WindowsPath": "C:\\projects\\bullbear",
            "MacPath": "",
            "LinuxPath": "",
            "CreatedAt": "2025-01-01T00:00:00+00:00",
            "UpdatedAt": "2025-01-01T00:00:00+00:00"
          }
        ]
        """;
        await File.WriteAllTextAsync(_tempFile, oldJson);

        var store = new JsonProjectStore(_tempFile);
        var loaded = await store.LoadAsync();

        Assert.Single(loaded);
        var project = loaded[0];
        Assert.Equal("bullbear", project.Name);
        Assert.Equal("illeArts-Finance", project.Owner);
        // New fields default safely instead of throwing or fabricating a link.
        Assert.Equal(string.Empty, project.RepositoryWebUrl);
        Assert.Equal(string.Empty, project.RepositoryOwner);
        Assert.Equal(string.Empty, project.RepositoryName);
        Assert.Equal(RemoteSource.Unknown, project.RemoteSource);
        Assert.False(project.HasGitHubLink);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsNewGitHubLinkFields()
    {
        var store = new JsonProjectStore(_tempFile);
        var original = new ManagedProject
        {
            Name = "bullbear",
            RepositoryOwner = "illeArts-Finance",
            RepositoryName = "bullbear",
            RepositoryWebUrl = "https://github.com/illeArts-Finance/bullbear",
            RemoteSource = RemoteSource.GitOrigin
        };

        await store.SaveAsync(new[] { original });
        var loaded = await store.LoadAsync();

        Assert.Single(loaded);
        Assert.Equal("illeArts-Finance", loaded[0].RepositoryOwner);
        Assert.Equal("bullbear", loaded[0].RepositoryName);
        Assert.Equal("https://github.com/illeArts-Finance/bullbear", loaded[0].RepositoryWebUrl);
        Assert.Equal(RemoteSource.GitOrigin, loaded[0].RemoteSource);
        Assert.True(loaded[0].HasGitHubLink);
    }

    [Fact]
    public void HasGitHubLink_IsNotSerialized()
    {
        var project = new ManagedProject { RepositoryWebUrl = "https://github.com/illeArts-Finance/bullbear" };
        var json = JsonSerializer.Serialize(project);
        Assert.DoesNotContain("HasGitHubLink", json);
    }

    [Fact]
    public async Task RemovingProjectFromList_DoesNotDeleteLocalFolderOrFiles()
    {
        Directory.CreateDirectory(_tempFolder);
        var markerFile = Path.Combine(_tempFolder, "README.md");
        await File.WriteAllTextAsync(markerFile, "still here");

        var store = new JsonProjectStore(_tempFile);
        var project = new ManagedProject
        {
            Name = "bullbear",
            RepositoryOwner = "illeArts-Finance",
            RepositoryName = "bullbear",
            RepositoryWebUrl = "https://github.com/illeArts-Finance/bullbear",
            RemoteSource = RemoteSource.GitOrigin,
            WindowsPath = _tempFolder,
            LinuxPath = _tempFolder,
            MacPath = _tempFolder
        };
        var projects = new List<ManagedProject> { project };
        await store.SaveAsync(projects);

        // This is exactly what "Aus Manager entfernen" does at the storage
        // level: remove the entry from the in-memory list and persist —
        // nothing here ever touches the filesystem the project points at.
        projects.Remove(project);
        await store.SaveAsync(projects);

        var reloaded = await store.LoadAsync();
        Assert.Empty(reloaded);

        // The real folder and its contents must be completely untouched.
        Assert.True(Directory.Exists(_tempFolder));
        Assert.True(File.Exists(markerFile));
        Assert.Equal("still here", await File.ReadAllTextAsync(markerFile));
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
        if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, recursive: true);
    }
}
