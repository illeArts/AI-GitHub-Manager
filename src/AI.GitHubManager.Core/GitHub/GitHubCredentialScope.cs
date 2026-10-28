namespace AI.GitHubManager.Core.GitHub;

/// <summary>Process-only GitHub authentication. Never writes global or repository configuration.</summary>
public static class GitHubCredentialScope
{
    public static string[] Apply(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray();
        var index = 0;
        while (index < args.Length && args[index] is "-c" or "-C") index += 2;
        if (index >= args.Length || args[index] is not ("clone" or "fetch" or "pull" or "push" or "ls-remote")) return args;
        if (args.Contains("credential.https://github.com.helper=")) return args;
        // Git executes !helpers through its POSIX shell, including Git for Windows.
        var executable = GitHubCliService.GhExe.Replace('\\', '/').Replace("'", "'\\''");
        return ["-c", "credential.https://github.com.helper=",
            "-c", $"credential.https://github.com.helper=!'{executable}' auth git-credential",
            "-c", "url.https://github.com/.insteadOf=git@github.com:",
            "-c", "url.https://github.com/.insteadOf=ssh://git@github.com/", .. args];
    }
}
