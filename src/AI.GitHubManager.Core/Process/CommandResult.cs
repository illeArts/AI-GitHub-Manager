namespace AI.GitHubManager.Core.Process;

public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string FileName,
    string Arguments)
{
    public bool Success => ExitCode == 0;
    public string CombinedOutput => string.Join(Environment.NewLine, new[] { StandardOutput, StandardError }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
