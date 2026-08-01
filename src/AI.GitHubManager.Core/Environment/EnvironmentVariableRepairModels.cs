namespace AI.GitHubManager.Core.EnvironmentRepair;

/// <summary>Where an environment variable value was found.</summary>
public enum EnvironmentVariableScope
{
    /// <summary>The currently running process' own environment.</summary>
    Process,

    /// <summary>Per-user environment (HKCU\Environment on Windows).</summary>
    User,

    /// <summary>Machine-wide environment (HKLM on Windows). Diagnosed only, never changed automatically.</summary>
    System
}

/// <summary>One (variable name, scope) finding — never carries the raw value.</summary>
public sealed record EnvironmentVariableFinding(
    string Name,
    EnvironmentVariableScope Scope,
    bool Exists,
    string? MaskedValue);

/// <summary>Snapshot of where GH_TOKEN/GITHUB_TOKEN currently exist, before any repair.</summary>
public sealed record EnvironmentRepairPlan(IReadOnlyList<EnvironmentVariableFinding> Findings)
{
    public bool HasAnyProblematicValue => Findings.Any(f => f.Exists);

    public bool HasRepairableValue => Findings.Any(f => f.Exists && f.Scope != EnvironmentVariableScope.System);

    public bool HasSystemValue => Findings.Any(f => f.Exists && f.Scope == EnvironmentVariableScope.System);
}

/// <summary>Outcome of one repair step for one (variable, scope) pair.</summary>
public sealed record EnvironmentRepairStepResult(
    string Name,
    EnvironmentVariableScope Scope,
    bool Success,
    string Message);

/// <summary>Full result of a repair attempt.</summary>
public sealed record EnvironmentRepairResult(bool Success, IReadOnlyList<EnvironmentRepairStepResult> Steps)
{
    public string Summary => string.Join(Environment.NewLine,
        Steps.Select(s => $"{(s.Success ? "✅" : "❌")} {s.Name} ({s.Scope}): {s.Message}"));
}
