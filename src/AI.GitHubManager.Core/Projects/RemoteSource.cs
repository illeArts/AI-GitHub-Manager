namespace AI.GitHubManager.Core.Projects;

/// <summary>
/// How a project's GitHub link (<see cref="ManagedProject.RepositoryWebUrl"/> and friends)
/// was determined. Never "derived from the logged-in GitHub account" — that combination
/// (currently-authenticated username + local folder name) must never be used to fabricate
/// a repository URL. The link either comes from the real <c>git remote get-url origin</c>
/// (<see cref="GitOrigin"/>), a manual entry validated against a real GitHub URL
/// (<see cref="Manual"/>), or the GitHub CLI's own repository listing (<see cref="Imported"/>).
/// </summary>
public enum RemoteSource
{
    Unknown,
    GitOrigin,
    Manual,
    Imported
}
