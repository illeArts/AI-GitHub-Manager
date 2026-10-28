using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Remote;
using AI.GitHubManager.Data;

// Opt-in, networked smoke test. Never runs from the ordinary test suite or pushes.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ImportSmoke -- https://github.com/OWNER/REPOSITORY.git");
    return 2;
}
var remote = ProjectImportService.CanonicalRemote(args[0]);
var testParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ai-github-manager-live-import"));
var testRoot = Path.Combine(testParent, Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
var target = Path.Combine(testRoot, "private-project");
var runner = new CommandRunner();
var service = new ProjectImportService(new ProjectImportCommands(runner));
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var gitConfig = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".gitconfig");
var previousConfig = File.Exists(gitConfig) ? await File.ReadAllBytesAsync(gitConfig) : [];
var result = await service.ImportAsync(remote, target,
    _ => throw new InvalidOperationException("Test target must be new"), token: cancellation.Token);
if (result is null || string.IsNullOrWhiteSpace(result.RepositoryId)) throw new Exception("Missing identity");
var authenticated = new CommandRunner(useGitHubCliCredentials: true);
var status = await new GitService(authenticated).GetStatusAsync(result.Path);
if (!status.IsRepository || !Directory.Exists(Path.Combine(target, ".git"))) throw new Exception("Clone verification failed");
var project = new ManagedProject { Name = "isolated-private-test", RemoteUrl = result.RemoteUrl, GitHubRepositoryId = result.RepositoryId };
if (OperatingSystem.IsWindows()) project.WindowsPath = result.Path;
else if (OperatingSystem.IsMacOS()) project.MacPath = result.Path;
else project.LinuxPath = result.Path;
var store = new JsonProjectStore(Path.Combine(testRoot, "projects.json"));
await store.SaveAsync([project]);
if ((await store.LoadAsync()).Single().GetPathForCurrentPlatform() != result.Path) throw new Exception("Persistence failed");
var linked = await service.ImportAsync(result.RemoteUrl, target, _ => Task.FromResult(ImportChoice.Link), token: cancellation.Token);
if (linked?.RepositoryId != result.RepositoryId) throw new Exception("Link verification failed");
// Prove that an SSH-configured origin can use the existing gh HTTPS login without rewriting its stored URL.
var ssh = RemoteUrlNormalizer.Parse(remote)!.CanonicalSshUrl;
var setRemote = await runner.RunAsync("git", ["remote", "set-url", "origin", ssh], target, cancellation.Token);
if (!setRemote.Success) throw new Exception("Test origin setup failed");
var pull = await new SafePullService(authenticated).PullAsync(target, warnOnlyOnLocalChanges: true, cancellation.Token);
if (!pull.Success) throw new Exception("Authenticated safe pull failed");
var storedRemote = await runner.RunAsync("git", ["remote", "get-url", "origin"], target, cancellation.Token);
if (storedRemote.StandardOutput.Trim() != ssh) throw new Exception("Stored SSH origin changed");
var currentConfig = File.Exists(gitConfig) ? await File.ReadAllBytesAsync(gitConfig) : [];
if (!previousConfig.SequenceEqual(currentConfig)) throw new Exception("Global Git configuration changed during test");
Console.WriteLine("PASS: live clone, status, link, SSH-origin safe pull and atomic persistence; global configuration unchanged; no remote writes.");
if (!Path.GetFullPath(testRoot).StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe cleanup root");
foreach (var file in Directory.EnumerateFiles(testRoot, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
Directory.Delete(testRoot, recursive: true);
return 0;
