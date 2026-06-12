using AI.GitHubManager.Core.Git;
using Xunit;

namespace AI.GitHubManager.Tests;

public class GitErrorParserTests
{
    // ── Authentication failed ────────────────────────────────────────────────

    [Theory]
    [InlineData("remote: Authentication failed for 'https://github.com/...'")]
    [InlineData("fatal: could not read Username for 'https://github.com': terminal prompts disabled")]
    [InlineData("remote: Invalid username or password.")]
    public void Parse_AuthenticationFailed_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.AuthenticationFailed, result.Kind);
        Assert.NotNull(result.Hint);
    }

    // ── Repository not found ─────────────────────────────────────────────────

    [Theory]
    [InlineData("ERROR: Repository not found.")]
    [InlineData("fatal: repository 'https://github.com/owner/repo.git/' not found")]
    public void Parse_RepositoryNotFound_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.RepositoryNotFound, result.Kind);
    }

    // ── Workflow scope missing ───────────────────────────────────────────────

    [Fact]
    public void Parse_WorkflowScopeMissing_DetectedCorrectly()
    {
        const string output =
            "refusing to allow a Personal Access Token to create or update workflow `.github/workflows/ci.yml`";
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.WorkflowScopeMissing, result.Kind);
        Assert.NotNull(result.Hint);
    }

    // ── Non-fast-forward ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("! [rejected]        main -> main (non-fast-forward)")]
    [InlineData("error: failed to push some refs\nhint: Updates were rejected because the remote contains work")]
    public void Parse_NonFastForward_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.NonFastForward, result.Kind);
    }

    // ── Unrelated histories ──────────────────────────────────────────────────

    [Fact]
    public void Parse_UnrelatedHistories_DetectedCorrectly()
    {
        const string output =
            "fatal: refusing to merge unrelated histories";
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.UnrelatedHistories, result.Kind);
    }

    // ── Merge conflict ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("CONFLICT (content): Merge conflict in src/Foo.cs")]
    [InlineData("Automatic merge failed; fix conflicts and then commit the result.")]
    public void Parse_MergeConflict_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.MergeConflict, result.Kind);
        Assert.NotNull(result.Hint);
    }

    // ── Nothing to commit ────────────────────────────────────────────────────

    [Theory]
    [InlineData("nothing to commit, working tree clean")]
    [InlineData("nothing added to commit but untracked files present")]
    public void Parse_NothingToCommit_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.NothingToCommit, result.Kind);
        Assert.Null(result.Hint); // NothingToCommit has no hint by design
    }

    // ── Index locked ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_IndexLocked_DetectedCorrectly()
    {
        const string output =
            "fatal: Unable to create '/path/to/repo/.git/index.lock': File exists.";
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.IndexLocked, result.Kind);
    }

    // ── Network error ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fatal: Could not resolve host: github.com")]
    [InlineData("Failed to connect to github.com port 443")]
    [InlineData("SSL certificate problem: certificate has expired")]
    public void Parse_NetworkError_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.NetworkError, result.Kind);
    }

    // ── Unknown ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("some totally unknown error")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_UnknownError_ReturnsUnknown(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.Unknown, result.Kind);
    }

    // ── FriendlyMessage ──────────────────────────────────────────────────────

    [Fact]
    public void FriendlyMessage_ReturnsNullForUnknown()
    {
        Assert.Null(GitErrorParser.FriendlyMessage(GitErrorKind.Unknown));
    }

    [Theory]
    [InlineData(GitErrorKind.AuthenticationFailed)]
    [InlineData(GitErrorKind.MergeConflict)]
    [InlineData(GitErrorKind.NonFastForward)]
    [InlineData(GitErrorKind.WorkflowScopeMissing)]
    public void FriendlyMessage_ReturnsNonNullForKnownKinds(GitErrorKind kind)
    {
        Assert.NotNull(GitErrorParser.FriendlyMessage(kind));
    }
}
