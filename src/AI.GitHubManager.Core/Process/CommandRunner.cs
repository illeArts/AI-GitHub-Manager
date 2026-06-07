using System.ComponentModel;
using System.Diagnostics;

namespace AI.GitHubManager.Core.Process;

public sealed class CommandRunner
{
    /// <summary>
    /// Extra paths prepended to PATH on macOS.
    /// Apps launched from Finder / Dock only get the system default PATH
    /// (/usr/bin:/bin:/usr/sbin:/sbin), which misses Homebrew and Xcode CLT.
    /// </summary>
    private static readonly string MacExtraPaths =
        "/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:/usr/local/sbin";

    public async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
        => await RunCoreAsync(fileName, arguments, null, workingDirectory, cancellationToken);

    public async Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToArray();
        return await RunCoreAsync(fileName, string.Join(" ", args), args, workingDirectory, cancellationToken);
    }

    private async Task<CommandResult> RunCoreAsync(
        string fileName,
        string argumentText,
        IReadOnlyList<string>? argumentList,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = BuildStartInfo(fileName, argumentText, argumentList, workingDirectory, redirect: true);

            using var process = new System.Diagnostics.Process { StartInfo = info };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult(process.ExitCode, await stdoutTask, await stderrTask, fileName, argumentText);
        }
        catch (Win32Exception ex)
        {
            return new CommandResult(-1, string.Empty,
                $"Programm nicht gefunden oder konnte nicht gestartet werden: {fileName}\n{ex.Message}",
                fileName, argumentText);
        }
        catch (FileNotFoundException ex)
        {
            return new CommandResult(-1, string.Empty,
                $"Programm nicht gefunden: {fileName}\n{ex.Message}",
                fileName, argumentText);
        }
    }

    public Task<CommandResult> RunDetachedAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null)
    {
        try
        {
            var info = BuildStartInfo(fileName, arguments, null, workingDirectory, redirect: false);
            info.CreateNoWindow = false;
            System.Diagnostics.Process.Start(info);
            return Task.FromResult(new CommandResult(0, "Fenster/Prozess wurde gestartet.", string.Empty, fileName, arguments));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CommandResult(-1, string.Empty, ex.Message, fileName, arguments));
        }
    }

    /// <summary>
    /// Like <see cref="RunDetachedAsync"/> but passes each argument individually via
    /// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/> so that no
    /// shell quoting or double-escaping is needed. Crucial for macOS osascript calls
    /// that contain AppleScript strings with their own quotation marks.
    /// </summary>
    public Task<CommandResult> RunDetachedWithArgsAsync(
        string fileName,
        IEnumerable<string> args,
        string? workingDirectory = null)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName         = fileName,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                                       ? Environment.CurrentDirectory
                                       : workingDirectory,
                UseShellExecute        = false,
                RedirectStandardOutput = false,
                RedirectStandardError  = false,
                CreateNoWindow         = false,
            };
            foreach (var a in args)
                info.ArgumentList.Add(a);
            EnrichPath(info);
            System.Diagnostics.Process.Start(info);
            return Task.FromResult(new CommandResult(0, "Fenster/Prozess wurde gestartet.", string.Empty, fileName, string.Join(" ", args)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CommandResult(-1, string.Empty, ex.Message, fileName, string.Empty));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ProcessStartInfo BuildStartInfo(
        string fileName,
        string arguments,
        IReadOnlyList<string>? argumentList,
        string? workingDirectory,
        bool redirect)
    {
        var info = new ProcessStartInfo
        {
            FileName               = fileName,
            Arguments              = arguments,
            WorkingDirectory       = string.IsNullOrWhiteSpace(workingDirectory)
                                         ? Environment.CurrentDirectory
                                         : workingDirectory,
            RedirectStandardOutput = redirect,
            RedirectStandardError  = redirect,
            UseShellExecute        = false,
            CreateNoWindow         = redirect, // true for captured runs, false for detached
        };

        if (argumentList is not null)
        {
            info.Arguments = string.Empty;
            foreach (var argument in argumentList)
                info.ArgumentList.Add(argument);
        }

        EnrichPath(info);
        return info;
    }

    /// <summary>
    /// On macOS, prepend Homebrew and common tool paths so commands like
    /// <c>gh</c> and <c>git</c> work even when the app is launched from
    /// Finder/Dock where the shell profile is never sourced.
    /// </summary>
    private static void EnrichPath(ProcessStartInfo info)
    {
        if (!OperatingSystem.IsMacOS()) return;

        // Accessing info.Environment triggers a copy of the current env vars.
        var current = info.Environment.TryGetValue("PATH", out var p) ? p ?? string.Empty : string.Empty;

        // Only prepend paths not already present to avoid duplication.
        var toAdd = MacExtraPaths
            .Split(':')
            .Where(seg => !current.Split(':').Contains(seg, StringComparer.Ordinal));

        var enriched = string.Join(":", toAdd) + (current.Length > 0 ? ":" + current : string.Empty);
        info.Environment["PATH"] = enriched;
    }
}
