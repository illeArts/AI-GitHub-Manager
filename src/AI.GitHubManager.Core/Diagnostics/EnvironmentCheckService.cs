using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.GitHub;

namespace AI.GitHubManager.Core.Diagnostics;

public sealed class EnvironmentCheckService
{
    private readonly GitService _git;
    private readonly GitHubCliService _gh;

    public EnvironmentCheckService(GitService git, GitHubCliService gh)
    {
        _git = git;
        _gh = gh;
    }

    public async Task<EnvironmentCheckResult> CheckAsync()
    {
        var gitVersion = await Safe(() => _git.VersionAsync());
        var ghVersion = await Safe(() => _gh.VersionAsync());

        var authService = new AuthenticationDiagnosticService(_gh);
        var auth = await authService.DiagnoseAsync();

        return new EnvironmentCheckResult(
            gitVersion.success,
            ghVersion.success,
            auth.IsUsable,
            gitVersion.output,
            ghVersion.output,
            auth.Summary + (string.IsNullOrWhiteSpace(auth.TechnicalDetails) ? string.Empty : "\n\n" + auth.TechnicalDetails),
            auth);
    }

    private static async Task<(bool success, string output)> Safe(Func<Task<Process.CommandResult>> action)
    {
        try
        {
            var result = await action();
            return (result.Success, result.CombinedOutput.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
