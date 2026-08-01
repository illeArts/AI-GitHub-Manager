using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Build;

/// <summary>
/// Creates a distributable installer for a managed project on the current
/// platform. On Windows this publishes a self-contained single-file build
/// and compiles it with Inno Setup 6 (installer/windows/setup.iss). On
/// macOS it invokes the repository's own build-installer-mac.sh.
///
/// Windows note: the repo's local build-installer-win.bat is a personal,
/// git-ignored helper script (see .gitignore — *.bat is intentionally never
/// committed) that pauses for user input on completion/failure. Running it
/// non-interactively from this app would hang forever waiting for a key
/// press that never comes, so the equivalent steps are implemented directly
/// here instead of shelling out to that script.
/// </summary>
public sealed class InstallerBuildService
{
    private readonly CommandRunner _runner;

    public InstallerBuildService(CommandRunner runner) => _runner = runner;

    public bool IsSupportedOnCurrentPlatform => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public async Task<CommandResult> CreateInstallerAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows())
            return await CreateWindowsInstallerAsync(repositoryPath, cancellationToken);

        if (OperatingSystem.IsMacOS())
            return await CreateMacInstallerAsync(repositoryPath, cancellationToken);

        return new CommandResult(-1, string.Empty,
            "Installer-Erstellung wird auf diesem Betriebssystem nicht unterstützt (nur Windows/macOS).",
            "installer", string.Empty);
    }

    private async Task<CommandResult> CreateWindowsInstallerAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var appProject = Path.Combine(repositoryPath, "src", "AI.GitHubManager.App", "AI.GitHubManager.App.csproj");
        if (!File.Exists(appProject))
            return new CommandResult(-1, string.Empty, $"App-Projekt nicht gefunden: {appProject}", "dotnet", "publish");

        var publishDir = Path.Combine(repositoryPath, "publish", "win-x64");

        var publish = await _runner.RunAsync("dotnet",
            [
                "publish", appProject,
                "-c", "Release",
                "-r", "win-x64",
                "--self-contained", "true",
                "-p:PublishSingleFile=true",
                "-p:IncludeNativeLibrariesForSelfExtract=true",
                "-p:EnableCompressionInSingleFile=true",
                "-o", publishDir
            ],
            repositoryPath, cancellationToken);

        if (!publish.Success) return publish;

        var iscc = FindInnoSetupCompiler();
        if (iscc is null)
        {
            return new CommandResult(0,
                publish.CombinedOutput.Trim() +
                "\n\nHinweis: Inno Setup 6 wurde nicht gefunden (https://jrsoftware.org/isinfo.php). " +
                $"Die publizierte .exe liegt bereits bereit unter: {publishDir}",
                string.Empty, "iscc", string.Empty);
        }

        var setupScript = Path.Combine(repositoryPath, "installer", "windows", "setup.iss");
        if (!File.Exists(setupScript))
            return new CommandResult(-1, publish.CombinedOutput, $"Installer-Skript nicht gefunden: {setupScript}", "iscc", setupScript);

        Directory.CreateDirectory(Path.Combine(repositoryPath, "dist"));

        var compile = await _runner.RunAsync(iscc, [setupScript], repositoryPath, cancellationToken);

        var combinedOutput = publish.CombinedOutput.Trim() + "\n\n" + compile.CombinedOutput.Trim();
        return new CommandResult(compile.ExitCode, combinedOutput, string.Empty, "iscc", setupScript);
    }

    private static string? FindInnoSetupCompiler()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Inno Setup 6", "ISCC.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Inno Setup 6", "ISCC.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Inno Setup 6", "ISCC.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<CommandResult> CreateMacInstallerAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var script = Path.Combine(repositoryPath, "build-installer-mac.sh");
        if (!File.Exists(script))
            return new CommandResult(-1, string.Empty, $"Installer-Skript nicht gefunden: {script}", "bash", script);

        return await _runner.RunAsync("bash", [script], repositoryPath, cancellationToken);
    }
}
