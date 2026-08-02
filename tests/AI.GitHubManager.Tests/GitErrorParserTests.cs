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

    // ── Pull-specific fast-forward failure (Teil B9 literal example) ────────────

    [Fact]
    public void Parse_PullNotFastForward_DetectedCorrectly()
    {
        const string output = "fatal: Not possible to fast-forward, aborting.";
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.PullNotFastForward, result.Kind);
        Assert.NotNull(result.Hint);
        Assert.NotEqual(GitErrorKind.NonFastForward, result.Kind); // distinct from a rejected Push
    }

    // ── No upstream ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fatal: The current branch feature/x has no upstream branch.")]
    [InlineData("To push the current branch and set the remote as upstream, use\n\ngit push --set-upstream origin feature/x")]
    public void Parse_NoUpstream_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.NoUpstream, result.Kind);
    }

    // ── Detached HEAD ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HEAD detached at a1b2c3d")]
    [InlineData("fatal: You are not currently on a branch.")]
    public void Parse_DetachedHead_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.DetachedHead, result.Kind);
    }

    // ── Interrupted merge/rebase ─────────────────────────────────────────────

    [Theory]
    [InlineData("error: you have not concluded your merge (MERGE_HEAD exists).")]
    [InlineData("error: could not apply a1b2c3d... rebase in progress; onto d4e5f6a")]
    public void Parse_InterruptedMergeOrRebase_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.InterruptedMergeOrRebase, result.Kind);
        Assert.NotNull(result.Hint);
    }

    // ── Permission error ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("error: unable to unlink old 'file.txt': Permission denied")]
    [InlineData("fatal: Operation not permitted")]
    public void Parse_PermissionError_DetectedCorrectly(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.Equal(GitErrorKind.PermissionError, result.Kind);
    }

    // ── Bilingual coverage (Teil A1: language switch must cover error text) ────

    [Theory]
    [InlineData("remote: Authentication failed for 'https://github.com/...'")]
    [InlineData("ERROR: Repository not found.")]
    [InlineData("CONFLICT (content): Merge conflict in src/Foo.cs")]
    [InlineData("fatal: Not possible to fast-forward, aborting.")]
    [InlineData("fatal: The current branch feature/x has no upstream branch.")]
    [InlineData("fatal: You are not currently on a branch.")]
    [InlineData("error: you have not concluded your merge (MERGE_HEAD exists).")]
    [InlineData("fatal: Operation not permitted")]
    public void Parse_KnownKinds_HaveNonEmptyGermanAndEnglishMessages(string output)
    {
        var result = GitErrorParser.Parse(output);
        Assert.NotEqual(GitErrorKind.Unknown, result.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Message(english: false)));
        Assert.False(string.IsNullOrWhiteSpace(result.Message(english: true)));
        Assert.NotEqual(result.Message(english: false), result.Message(english: true));
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
