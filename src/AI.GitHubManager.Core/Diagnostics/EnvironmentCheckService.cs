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
        var auth = await Safe(() => _gh.AuthStatusAsync());

        return new EnvironmentCheckResult(
            gitVersion.success,
            ghVersion.success,
            auth.success,
            gitVersion.output,
            ghVersion.output,
            auth.output);
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
