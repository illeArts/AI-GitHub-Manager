using System.Diagnostics;
using System.Runtime.Versioning;

namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Conservative, cross-platform fallback: treats <em>any</em> running process
/// named "git" (regardless of which repository it's working in) as a signal
/// that a lock might still be legitimately held. This over-blocks rather than
/// risking a wrong deletion — appropriate on macOS/Linux and as the Windows
/// detector's fallback when a more precise check is unavailable.
/// </summary>
public sealed class ConservativeGitProcessDetector : IGitProcessDetector
{
    public bool IsGitProcessActiveFor(string normalizedRepositoryPath)
    {
        try
        {
            var processes = Process.GetProcessesByName("git");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }
        catch
        {
            // Fail closed: if we can't even enumerate processes, assume one might be active.
            return true;
        }
    }
}

/// <summary>
/// Windows-specific, best-effort detector: uses WMI (<c>Win32_Process</c>) to read
/// each running <c>git.exe</c> process's command line and check whether it
/// references the repository path directly. This is inherently imprecise —
/// many git invocations set the working directory via the process' "start in"
/// folder rather than passing the path as a command-line argument, so a
/// negative WMI result does not prove no git process is working in this
/// repository. For that reason, a WMI result of "no matching process found"
/// is treated as informative but not conclusive on its own: it is combined
/// with the conservative "any git process at all" check as a safety net, and
/// any WMI failure (access denied, WMI unavailable, unexpected exception)
/// falls back to the conservative detector entirely.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGitProcessDetector : IGitProcessDetector
{
    private readonly ConservativeGitProcessDetector _fallback = new();

    public bool IsGitProcessActiveFor(string normalizedRepositoryPath)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE Name = 'git.exe'");
            using var results = searcher.Get();

            var sawAnyGitProcess = false;
            foreach (System.Management.ManagementObject process in results)
            {
                sawAnyGitProcess = true;
                var commandLine = process["CommandLine"] as string;
                if (!string.IsNullOrEmpty(commandLine) &&
                    commandLine.Contains(normalizedRepositoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // WMI ran successfully. If it saw git.exe processes but none of them
            // clearly referenced this path via their command line, we still can't
            // be certain (working directory isn't visible here) — but requiring an
            // explicit match would make removal too eager. Treating "any git.exe
            // running at all" as active is the safe (over-blocking) choice.
            return sawAnyGitProcess;
        }
        catch
        {
            // WMI unavailable, access denied, or any other failure → fail closed
            // via the conservative fallback rather than assuming "no process".
            return _fallback.IsGitProcessActiveFor(normalizedRepositoryPath);
        }
    }
}

/// <summary>Picks the best available <see cref="IGitProcessDetector"/> for the current OS.</summary>
public static class GitProcessDetectorFactory
{
    public static IGitProcessDetector CreateDefault() =>
        OperatingSystem.IsWindows()
            ? new WindowsGitProcessDetector()
            : new ConservativeGitProcessDetector();
}
