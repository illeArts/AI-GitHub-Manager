using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AI.GitHubManager.Core.EnvironmentRepair;

/// <summary>
/// Diagnoses and repairs the specific failure mode where a stale/invalid
/// GH_TOKEN or GITHUB_TOKEN in the Windows user environment hides an otherwise
/// valid `gh` keyring login.
///
/// Safety rules (non-negotiable):
///   - Never touches the `gh` keyring/credential store — only the two named
///     environment variables.
///   - Never modifies HKLM (system-wide) variables; those are diagnosed only
///     and require the user to change them manually with elevation.
///   - Never logs or displays a raw token value — only masked forms.
///   - A failed repair leaves the keyring untouched (nothing to roll back there;
///     removing an environment variable is the only mutation this class performs).
/// </summary>
public static class GitHubEnvironmentRepairService
{
    private static readonly string[] VariableNames = ["GITHUB_TOKEN", "GH_TOKEN"];

    /// <summary>Builds a snapshot of where the two variables currently exist.</summary>
    public static EnvironmentRepairPlan CreatePlan()
    {
        var findings = new List<EnvironmentVariableFinding>();

        foreach (var name in VariableNames)
        {
            var processValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            findings.Add(new EnvironmentVariableFinding(
                name, EnvironmentVariableScope.Process, !string.IsNullOrEmpty(processValue), Mask(processValue)));

            if (OperatingSystem.IsWindows())
            {
                var userValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                findings.Add(new EnvironmentVariableFinding(
                    name, EnvironmentVariableScope.User, !string.IsNullOrEmpty(userValue), Mask(userValue)));

                var systemValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
                findings.Add(new EnvironmentVariableFinding(
                    name, EnvironmentVariableScope.System, !string.IsNullOrEmpty(systemValue), Mask(systemValue)));
            }
        }

        return new EnvironmentRepairPlan(findings);
    }

    /// <summary>
    /// Removes GITHUB_TOKEN/GH_TOKEN from the current process' environment and,
    /// on Windows, from the per-user environment (HKCU\Environment) — but only
    /// the values that were actually found by <see cref="CreatePlan"/>.
    /// System (HKLM) values are reported but never changed. After a successful
    /// repair, this process can immediately call `gh`/`git` again with a clean
    /// environment; a Windows restart is not required.
    /// </summary>
    public static EnvironmentRepairResult Repair(EnvironmentRepairPlan plan)
    {
        var steps = new List<EnvironmentRepairStepResult>();

        foreach (var name in VariableNames)
        {
            var processFinding = Find(plan, name, EnvironmentVariableScope.Process);
            if (processFinding is { Exists: true })
            {
                try
                {
                    Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
                    steps.Add(new EnvironmentRepairStepResult(name, EnvironmentVariableScope.Process, true,
                        "Aus der laufenden Prozessumgebung entfernt."));
                }
                catch (Exception ex)
                {
                    steps.Add(new EnvironmentRepairStepResult(name, EnvironmentVariableScope.Process, false,
                        $"Konnte nicht entfernt werden: {ex.Message}"));
                }
            }

            var userFinding = Find(plan, name, EnvironmentVariableScope.User);
            if (userFinding is { Exists: true })
            {
                if (OperatingSystem.IsWindows())
                    steps.Add(RemoveUserVariableWindows(name));
                else
                    steps.Add(new EnvironmentRepairStepResult(name, EnvironmentVariableScope.User, false,
                        "Automatische Bereinigung der Benutzerumgebung ist nur unter Windows verfügbar."));
            }

            var systemFinding = Find(plan, name, EnvironmentVariableScope.System);
            if (systemFinding is { Exists: true })
            {
                steps.Add(new EnvironmentRepairStepResult(name, EnvironmentVariableScope.System, false,
                    "Als Systemvariable (HKLM) gefunden. Wird nicht automatisch geändert – dafür sind " +
                    "Administratorrechte und eine ausdrückliche Bestätigung nötig."));
            }
        }

        if (steps.Count == 0)
        {
            steps.Add(new EnvironmentRepairStepResult("—", EnvironmentVariableScope.Process, true,
                "Keine problematischen Werte gefunden – nichts zu tun."));
        }

        if (OperatingSystem.IsWindows())
            BroadcastEnvironmentChangeSafe();

        bool anyRepairableStepFailed = steps.Any(s => !s.Success && s.Scope != EnvironmentVariableScope.System);
        return new EnvironmentRepairResult(!anyRepairableStepFailed, steps);
    }

    [SupportedOSPlatform("windows")]
    private static EnvironmentRepairStepResult RemoveUserVariableWindows(string name)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
            return new EnvironmentRepairStepResult(name, EnvironmentVariableScope.User, true,
                "Aus der Benutzer-Umgebung (HKCU\\Environment) entfernt.");
        }
        catch (Exception ex)
        {
            return new EnvironmentRepairStepResult(name, EnvironmentVariableScope.User, false,
                $"Konnte nicht aus der Benutzer-Umgebung entfernt werden: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts WM_SETTINGCHANGE so other running applications notice the
    /// environment change without requiring a logoff/restart. Best-effort —
    /// failure here does not affect whether the repair itself succeeded.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void BroadcastEnvironmentChangeSafe()
    {
        try
        {
            const int HWND_BROADCAST = 0xffff;
            const uint WM_SETTINGCHANGE = 0x001A;
            const uint SMTO_ABORTIFHUNG = 0x0002;
            NativeMethods.SendMessageTimeout(
                new IntPtr(HWND_BROADCAST), WM_SETTINGCHANGE, IntPtr.Zero, "Environment",
                SMTO_ABORTIFHUNG, 5000, out _);
        }
        catch
        {
            // Notification is best-effort only; the running process already has
            // the cleaned environment regardless of whether other apps are notified.
        }
    }

    private static EnvironmentVariableFinding? Find(EnvironmentRepairPlan plan, string name, EnvironmentVariableScope scope)
        => plan.Findings.FirstOrDefault(f => f.Name == name && f.Scope == scope);

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length <= 8 ? "****" : $"{value[..4]}****{value[^4..]}";
    }
}

[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeoutMilliseconds, out IntPtr result);
}
