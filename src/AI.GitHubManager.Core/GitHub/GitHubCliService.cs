using System.Text.Json;
using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.GitHub;

public sealed class GitHubCliService
{
    private readonly CommandRunner _runner;

    // Resolved once at startup so all calls use the same path
    private static readonly string GhExe = ResolveGhExe();

    public GitHubCliService(CommandRunner runner) => _runner = runner;

    /// <summary>
    /// Finds the gh executable.
    /// On Windows the process PATH often misses the GitHub CLI directory when
    /// launched from within Visual Studio (PATH is captured at VS startup, before
    /// winget finishes adding the entry). We fall back to the two common install
    /// locations so the app works without requiring a VS restart.
    /// </summary>
    private static string ResolveGhExe()
    {
        string[] candidates;

        if (OperatingSystem.IsWindows())
        {
            candidates =
            [
                // winget / MSI default install location
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                             "GitHub CLI", "gh.exe"),
                // winget app-execution alias (AppData\Local\Microsoft\WindowsApps)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             "Microsoft", "WindowsApps", "gh.exe"),
                // Scoop
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                             "scoop", "shims", "gh.exe"),
            ];
        }
        else if (OperatingSystem.IsMacOS())
        {
            // CommandRunner already enriches PATH with these directories,
            // but we also resolve statically so the Terminal command strings
            // use the full path (avoids issues with osascript's limited PATH).
            candidates =
            [
                "/opt/homebrew/bin/gh",       // Homebrew on Apple Silicon
                "/usr/local/bin/gh",           // Homebrew on Intel Mac
                "/usr/bin/gh",                 // rare system install
            ];
        }
        else
        {
            return "gh"; // Linux: rely on PATH (CommandRunner enriches it on macOS already)
        }

        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        return "gh"; // fall back to PATH lookup
    }

    public Task<CommandResult> VersionAsync() => _runner.RunAsync(GhExe, ["--version"]);
    public Task<CommandResult> AuthStatusAsync() => _runner.RunAsync(GhExe, ["auth", "status"]);
    public Task<CommandResult> AuthLoginAsync() => _runner.RunAsync(GhExe, ["auth", "login", "--web", "--scopes", "repo,workflow"]);
    public Task<CommandResult> SetupGitAsync() => _runner.RunAsync(GhExe, ["auth", "setup-git"]);
    public Task<CommandResult> RefreshWorkflowScopeAsync() => _runner.RunAsync(GhExe, ["auth", "refresh", "--scopes", "repo,workflow"]);

    /// <summary>
    /// Runs `gh auth status` again in a child process that has GH_TOKEN and GITHUB_TOKEN
    /// removed from ITS environment only. The real user/system environment is never touched.
    /// Used to detect a valid `gh` keyring login hiding underneath a bad environment token.
    /// </summary>
    public Task<CommandResult> AuthStatusWithCleanedEnvironmentAsync() =>
        _runner.RunAsync(GhExe, ["auth", "status"], null, new Dictionary<string, string?>
        {
            ["GH_TOKEN"] = null,
            ["GITHUB_TOKEN"] = null,
        });

    public Task<CommandResult> OpenAuthLoginTerminalAsync()
    {
        // Use the resolved full path in the terminal command so the new cmd window
        // also works even when the user PATH is stale.
        var ghCmd = GhExe.Contains(' ') ? $"\"{GhExe}\"" : GhExe;
        var command = $"{ghCmd} auth login --web --scopes repo,workflow && {ghCmd} auth setup-git";

        if (OperatingSystem.IsWindows())
        {
            return _runner.RunDetachedAsync("cmd.exe", $"/c start \"AI GitHub Login\" cmd /k \"{command} && echo. && echo GitHub Login fertig. Dieses Fenster kann geschlossen werden. && pause\"");
        }

        if (OperatingSystem.IsMacOS())
        {
            // Use ArgumentList so .NET doesn't re-parse/mangle the AppleScript string.
            // Inside AppleScript double-quotes are the only thing that needs escaping (\").
            // ghCmd is a bare path like /opt/homebrew/bin/gh — no spaces, no extra quoting.
            var appleScript = $"tell application \"Terminal\" to do script \"{command}; echo; echo GitHub Login fertig. Fenster kann geschlossen werden.\"";
            return _runner.RunDetachedWithArgsAsync("osascript", ["-e", appleScript]);
        }

        return _runner.RunDetachedAsync("sh", $"-c \"x-terminal-emulator -e 'bash -lc \\\"{command}; echo; echo GitHub Login fertig.; read -p Press-Enter\\\"'\"");
    }

    public Task<CommandResult> OpenInstallGitHubCliTerminalAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            return _runner.RunDetachedAsync("cmd.exe", "/c start \"GitHub CLI Installation\" cmd /k \"winget install --id GitHub.cli --source winget && echo. && echo Danach Visual Studio/App neu starten. && pause\"");
        }

        if (OperatingSystem.IsMacOS())
        {
            var appleScript = "tell application \"Terminal\" to do script \"brew install gh; echo; echo Danach App neu starten.\"";
            return _runner.RunDetachedWithArgsAsync("osascript", ["-e", appleScript]);
        }

        return _runner.RunDetachedAsync("sh", "-c \"x-terminal-emulator -e 'bash -lc \\\"echo Bitte GitHub CLI installieren: https://cli.github.com/; read -p Press-Enter\\\"'\"");
    }

    public async Task<IReadOnlyList<GitHubRepositoryInfo>> ListRepositoriesAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = await _runner.RunAsync(GhExe, ["repo", "list", "--limit", safeLimit, "--json", "nameWithOwner,name,owner,url,defaultBranchRef"], cancellationToken: cancellationToken);
        if (!result.Success) return Array.Empty<GitHubRepositoryInfo>();

        try
        {
            using var doc = JsonDocument.Parse(result.StandardOutput);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<GitHubRepositoryInfo>();

            var repos = new List<GitHubRepositoryInfo>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var nameWithOwner = GetString(item, "nameWithOwner");
                var name = GetString(item, "name");
                var owner = item.TryGetProperty("owner", out var ownerElement)
                            && ownerElement.ValueKind == JsonValueKind.Object
                    ? GetString(ownerElement, "login")
                    : string.Empty;
                var url = GetString(item, "url");
                var branch = "main";
                if (item.TryGetProperty("defaultBranchRef", out var branchElement)
                    && branchElement.ValueKind == JsonValueKind.Object)
                {
                    branch = GetString(branchElement, "name");
                    if (string.IsNullOrWhiteSpace(branch)) branch = "main";
                }

                if (!string.IsNullOrWhiteSpace(nameWithOwner) && !string.IsNullOrWhiteSpace(url))
                    repos.Add(new GitHubRepositoryInfo(nameWithOwner, name, owner, url, branch));
            }
            return repos;
        }
        catch (JsonException)
        {
            return Array.Empty<GitHubRepositoryInfo>();
        }
    }

    private static string GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
}
