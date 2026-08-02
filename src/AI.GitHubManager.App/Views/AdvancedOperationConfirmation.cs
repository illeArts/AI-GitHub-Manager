using AI.GitHubManager.Core.Operations;

namespace AI.GitHubManager.App.Views;

/// <summary>
/// What the confirmation dialog needs to know to name repo/branch/scope
/// explicitly (Teil B7: "unter Nennung von Repository, Branch, betroffenem
/// Umfang, Wiederherstellbarkeit").
/// </summary>
public sealed record AdvancedOperationConfirmationRequest(
    GitOperationDefinition Operation,
    string RepositoryPath,
    string Branch);

/// <summary>
/// Result of the confirmation dialog. <see cref="Confirmed"/> is only ever
/// true after the user actively clicked Confirm (never via Enter, never
/// pre-checked) and, for Dangerous operations, only after they typed the
/// exact branch name. <see cref="TargetRef"/> carries the user-entered
/// target ref/branch/commit for operations that need one (Reset, Hard
/// Reset, Rebase, Cherry-Pick) — null for operations that don't.
/// </summary>
public sealed record AdvancedOperationConfirmationResult(bool Confirmed, string? TargetRef);
