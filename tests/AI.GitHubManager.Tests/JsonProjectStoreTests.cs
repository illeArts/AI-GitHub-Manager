using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Data;
using Xunit;

namespace AI.GitHubManager.Tests;

public class JsonProjectStoreTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    [Fact]
    public async Task LoadAsync_ReturnsEmptyListWhenFileDoesNotExist()
    {
        File.Delete(_tempFile); // ensure it doesn't exist
        var store  = new JsonProjectStore(_tempFile);
        var result = await store.LoadAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsProjectProperties()
    {
        var store    = new JsonProjectStore(_tempFile);
        var original = new ManagedProject
        {
            Name          = "TestRepo",
            Owner         = "testowner",
            RemoteUrl     = "https://github.com/testowner/testrepo.git",
            DefaultBranch = "develop",
            WindowsPath   = @"C:\projects\testrepo"
        };

        await store.SaveAsync(new[] { original });
        var loaded = await store.LoadAsync();

        Assert.Single(loaded);
        Assert.Equal(original.Name,          loaded[0].Name);
        Assert.Equal(original.Owner,         loaded[0].Owner);
        Assert.Equal(original.RemoteUrl,     loaded[0].RemoteUrl);
        Assert.Equal(original.DefaultBranch, loaded[0].DefaultBranch);
        Assert.Equal(original.WindowsPath,   loaded[0].WindowsPath);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesMultipleProjects()
    {
        var store = new JsonProjectStore(_tempFile);
        var projects = new[]
        {
            new ManagedProject { Name = "Alpha" },
            new ManagedProject { Name = "Beta"  },
            new ManagedProject { Name = "Gamma" }
        };

        await store.SaveAsync(projects);
        var loaded = await store.LoadAsync();

        Assert.Equal(3, loaded.Count);
        Assert.Equal("Alpha", loaded[0].Name);
        Assert.Equal("Beta",  loaded[1].Name);
        Assert.Equal("Gamma", loaded[2].Name);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingData()
    {
        var store = new JsonProjectStore(_tempFile);
        await store.SaveAsync(new[] { new ManagedProject { Name = "Old" } });
        await store.SaveAsync(new[] { new ManagedProject { Name = "New1" }, new ManagedProject { Name = "New2" } });

        var loaded = await store.LoadAsync();
        Assert.Equal(2, loaded.Count);
        Assert.DoesNotContain(loaded, p => p.Name == "Old");
    }

    [Fact]
    public async Task SaveAsync_EmptyList_LoadReturnsEmptyList()
    {
        var store = new JsonProjectStore(_tempFile);
        await store.SaveAsync(Array.Empty<ManagedProject>());
        var loaded = await store.LoadAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadAsync_ReturnsEmptyListWhenJsonIsCorrupt()
    {
        await File.WriteAllTextAsync(_tempFile, "{ not valid json");
        var store = new JsonProjectStore(_tempFile);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}
