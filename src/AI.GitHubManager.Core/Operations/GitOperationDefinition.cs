namespace AI.GitHubManager.Core.Operations;

/// <summary>
/// Describes one Git operation the way a non-expert user should see it
/// (Teil B3): a stable id, a bilingual understandable title, the underlying
/// technical command shown secondary/smaller, a short description, and the
/// structured explanation fields shown directly under the selection
/// (Teil B4): "Geeignet für" / "Was wird verändert?" / "Was bleibt
/// unverändert?" / risk text / risk level.
///
/// Text lives exclusively here — never duplicated ad hoc in ViewModels or
/// code-behind (Teil B3, last line).
/// </summary>
public sealed record GitOperationDefinition
{
    /// <summary>Stable identifier, used for persistence — never shown to the user.</summary>
    public required string Id { get; init; }

    public required string TitleDe { get; init; }
    public required string TitleEn { get; init; }

    /// <summary>The technical command or execution strategy, shown secondary to the title (Teil B3/B4).</summary>
    public required string TechnicalCommand { get; init; }

    public required string DescriptionDe { get; init; }
    public required string DescriptionEn { get; init; }

    public required string SuitableForDe { get; init; }
    public required string SuitableForEn { get; init; }

    public required string WhatChangesDe { get; init; }
    public required string WhatChangesEn { get; init; }

    public required string WhatStaysDe { get; init; }
    public required string WhatStaysEn { get; init; }

    public required string RiskDe { get; init; }
    public required string RiskEn { get; init; }

    public required GitOperationRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// True for operations listed under "Erweiterte Befehle" (Teil B2) —
    /// never shown in the normal operation dropdown, never auto-selected,
    /// always require explicit extra confirmation before execution.
    /// </summary>
    public bool IsAdvanced { get; init; }

    /// <summary>
    /// True only for the small number of normal operations that are
    /// already wired to a real, tested command in this milestone (Status,
    /// Aktualisieren, Commit erstellen und hochladen). All other operations
    /// exist as fully described model entries but do not execute anything
    /// yet — later milestones (Vorabprüfungen, Schutzmechanismen) add real
    /// execution for the rest. Never claim an operation runs when it does not.
    /// </summary>
    public bool IsExecutable { get; init; }

    public string Title(bool english) => english ? TitleEn : TitleDe;
    public string Description(bool english) => english ? DescriptionEn : DescriptionDe;
    public string SuitableFor(bool english) => english ? SuitableForEn : SuitableForDe;
    public string WhatChanges(bool english) => english ? WhatChangesEn : WhatChangesDe;
    public string WhatStays(bool english) => english ? WhatStaysEn : WhatStaysDe;
    public string Risk(bool english) => english ? RiskEn : RiskDe;
}
