using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Build;

/// <summary>Which build system, if any, was detected in a project folder.</summary>
public enum ProjectBuildSystem
{
    Unknown,
    DotNet
}

/// <summary>Result of detecting a build system in a project folder.</summary>
public sealed record BuildSystemInfo(ProjectBuildSystem System, string? SolutionOrProjectPath);

/// <summary>
/// Runs `dotnet build` / `dotnet test` for a managed project. Used by the
/// "Build, Test &amp; Push" self-service action so the app never pushes code
/// that doesn't build or whose tests fail.
/// </summary>
public sealed class ProjectBuildService
{
    private readonly CommandRunner _runner;

    public ProjectBuildService(CommandRunner runner) => _runner = runner;

    /// <summary>
    /// Looks for a .NET solution or project file directly in the given folder.
    /// Only top-level files are considered — deeply nested solutions must be
    /// built manually.
    /// </summary>
    public static BuildSystemInfo Detect(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return new BuildSystemInfo(ProjectBuildSystem.Unknown, null);

        var sln = Directory.EnumerateFiles(repositoryPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln is not null)
            return new BuildSystemInfo(ProjectBuildSystem.DotNet, sln);

        var csproj = Directory.EnumerateFiles(repositoryPath, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (csproj is not null)
            return new BuildSystemInfo(ProjectBuildSystem.DotNet, csproj);

        return new BuildSystemInfo(ProjectBuildSystem.Unknown, null);
    }

    public async Task<CommandResult> BuildAsync(string repositoryPath, string? customArguments = null, CancellationToken cancellationToken = default)
    {
        if (Detect(repositoryPath).System == ProjectBuildSystem.Unknown)
            return NoBuildSystemResult("build");

        var args = string.IsNullOrWhiteSpace(customArguments) ? "build -c Release" : customArguments;
        return await _runner.RunAsync("dotnet", args, repositoryPath, cancellationToken);
    }

    public async Task<CommandResult> TestAsync(string repositoryPath, string? customArguments = null, CancellationToken cancellationToken = default)
    {
        if (Detect(repositoryPath).System == ProjectBuildSystem.Unknown)
            return NoBuildSystemResult("test");

        var args = string.IsNullOrWhiteSpace(customArguments) ? "test -c Release --no-build" : customArguments;
        return await _runner.RunAsync("dotnet", args, repositoryPath, cancellationToken);
    }

    private static CommandResult NoBuildSystemResult(string step) => new(
        -1, string.Empty,
        $"Kein unterstütztes .NET-Build-System gefunden (keine .sln/.csproj im Projektordner). '{step}' übersprungen.",
        "dotnet", step);
}
