namespace AI.GitHubManager.Core.Projects;

public sealed class ManagedProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string RemoteUrl { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public string WindowsPath { get; set; } = string.Empty;
    public string MacPath { get; set; } = string.Empty;
    public string LinuxPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string GetPathForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return WindowsPath;
        if (OperatingSystem.IsMacOS()) return MacPath;
        return LinuxPath;
    }
}
