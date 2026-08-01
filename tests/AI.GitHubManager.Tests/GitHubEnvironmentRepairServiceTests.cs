using AI.GitHubManager.Core.EnvironmentRepair;
using Xunit;

namespace AI.GitHubManager.Tests;

/// <summary>
/// Registry (HKCU/HKLM) access is Windows-only and guarded internally by
/// OperatingSystem.IsWindows(). These tests exercise the OS-agnostic parts
/// (process-scope variables, masking, "nothing to do") on every platform,
/// so they run in CI regardless of the build agent's OS.
/// </summary>
public sealed class GitHubEnvironmentRepairServiceTests : IDisposable
{
    public GitHubEnvironmentRepairServiceTests() => ClearProcessVariables();

    public void Dispose() => ClearProcessVariables();

    private static void ClearProcessVariables()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GH_TOKEN", null, EnvironmentVariableTarget.Process);
    }

    // 1. Only a process variable is present.
    [Fact]
    public void Repair_RemovesProcessOnlyVariable()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "ghp_sometokenvalue1234", EnvironmentVariableTarget.Process);

        var plan = GitHubEnvironmentRepairService.CreatePlan();
        Assert.True(plan.HasAnyProblematicValue);

        var result = GitHubEnvironmentRepairService.Repair(plan);

        Assert.True(result.Success);
        Assert.Null(Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.Process));
        Assert.Contains(result.Steps, s => s.Name == "GITHUB_TOKEN" && s.Scope == EnvironmentVariableScope.Process && s.Success);
    }

    // 3. Variable not present at all → nothing to do, no error.
    [Fact]
    public void Repair_NoVariablesPresent_ReportsNothingToDo()
    {
        var plan = GitHubEnvironmentRepairService.CreatePlan();
        Assert.False(plan.HasAnyProblematicValue);

        var result = GitHubEnvironmentRepairService.Repair(plan);

        Assert.True(result.Success);
        Assert.Single(result.Steps);
    }

    // 7. Logs / step summary never contain the raw token value.
    [Fact]
    public void Repair_NeverExposesRawTokenValue()
    {
        const string secret = "ghp_totallysecretvalue9999";
        Environment.SetEnvironmentVariable("GH_TOKEN", secret, EnvironmentVariableTarget.Process);

        var plan = GitHubEnvironmentRepairService.CreatePlan();
        Assert.DoesNotContain(plan.Findings, f => f.MaskedValue == secret);

        var result = GitHubEnvironmentRepairService.Repair(plan);
        Assert.DoesNotContain(secret, result.Summary);
    }

    // Masked values only ever show a short prefix/suffix, never the full token.
    [Fact]
    public void CreatePlan_MasksValuesRatherThanExposingThem()
    {
        const string secret = "ghp_abcdefghijklmnopqrstuvwxyz";
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", secret, EnvironmentVariableTarget.Process);

        var plan = GitHubEnvironmentRepairService.CreatePlan();
        var finding = plan.Findings.Single(f => f.Name == "GITHUB_TOKEN" && f.Scope == EnvironmentVariableScope.Process);

        Assert.True(finding.Exists);
        Assert.NotNull(finding.MaskedValue);
        Assert.Contains("****", finding.MaskedValue);
        Assert.DoesNotContain(secret, finding.MaskedValue);
    }
}
