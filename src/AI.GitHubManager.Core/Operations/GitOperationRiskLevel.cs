namespace AI.GitHubManager.Core.Operations;

/// <summary>
/// Risk classification shown to the user for every Git operation (Teil B5).
/// Always paired with visible text in the UI — risk must never be conveyed
/// by color alone.
/// </summary>
public enum GitOperationRiskLevel
{
    /// <summary>Read-only or trivially reversible. No working files are put at risk.</summary>
    Safe,

    /// <summary>Changes local state or the remote in a normal, expected way; conflicts are possible.</summary>
    Caution,

    /// <summary>Rewrites history or otherwise requires Git experience to use safely.</summary>
    Advanced,

    /// <summary>Can destroy local work or overwrite remote history. Requires explicit, deliberate confirmation.</summary>
    Dangerous,
}
