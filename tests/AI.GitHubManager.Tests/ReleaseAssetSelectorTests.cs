using AI.GitHubManager.Core.Update;
using Xunit;

namespace AI.GitHubManager.Tests;

public sealed class ReleaseAssetSelectorTests
{
    private static readonly ReleaseAsset[] TypicalReleaseAssets =
    {
        new("AI_GitHub_Manager_Setup_1.6.2_win-x64.exe", "https://example.invalid/win.exe"),
        new("AI_GitHub_Manager_Setup_1.6.2_win-x64.exe.sha256", "https://example.invalid/win.exe.sha256"),
        new("AI-GitHub-Manager-v1.6.2-macos-x64.zip", "https://example.invalid/mac-x64.zip"),
        new("AI-GitHub-Manager-v1.6.2-macos-x64.zip.sha256", "https://example.invalid/mac-x64.zip.sha256"),
        new("AI-GitHub-Manager-v1.6.2-macos-arm64.zip", "https://example.invalid/mac-arm64.zip"),
        new("AI-GitHub-Manager-v1.6.2-macos-arm64.zip.sha256", "https://example.invalid/mac-arm64.zip.sha256"),
        new("AI-GitHub-Manager-v1.6.2-linux-x64.tar.gz", "https://example.invalid/linux.tar.gz"),
        new("AI-GitHub-Manager-v1.6.2-linux-x64.tar.gz.sha256", "https://example.invalid/linux.tar.gz.sha256"),
    };

    [Fact]
    public void WindowsX64_SelectsWindowsInstaller()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.Windows, UpdateArchitecture.X64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.NotNull(selected);
        Assert.Equal("AI_GitHub_Manager_Setup_1.6.2_win-x64.exe", selected!.Name);
    }

    [Fact]
    public void MacOSX64_SelectsMacIntelPackage()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.MacOS, UpdateArchitecture.X64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.NotNull(selected);
        Assert.Equal("AI-GitHub-Manager-v1.6.2-macos-x64.zip", selected!.Name);
    }

    [Fact]
    public void MacOSArm64_SelectsAppleSiliconPackage()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.MacOS, UpdateArchitecture.Arm64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.NotNull(selected);
        Assert.Equal("AI-GitHub-Manager-v1.6.2-macos-arm64.zip", selected!.Name);
    }

    [Fact]
    public void LinuxX64_SelectsLinuxPackage()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.Linux, UpdateArchitecture.X64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.NotNull(selected);
        Assert.Equal("AI-GitHub-Manager-v1.6.2-linux-x64.tar.gz", selected!.Name);
    }

    [Fact]
    public void UnsupportedArchitecture_ReturnsNull()
    {
        // e.g. Windows on Arm64 is not currently published.
        var platform = new PlatformDescriptor(UpdateOperatingSystem.Windows, UpdateArchitecture.Arm64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.Null(selected);
    }

    [Fact]
    public void MissingMatchingAsset_ReturnsNullInsteadOfWrongPlatform()
    {
        // Release only shipped a Windows installer this time.
        var assets = new[] { TypicalReleaseAssets[0] };
        var platform = new PlatformDescriptor(UpdateOperatingSystem.MacOS, UpdateArchitecture.Arm64);

        var selected = ReleaseAssetSelector.SelectFor(platform, assets);

        Assert.Null(selected);
    }

    [Fact]
    public void NeverReturnsWindowsAssetOnMacOS()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.MacOS, UpdateArchitecture.X64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.NotNull(selected);
        Assert.DoesNotContain("win-x64", selected!.Name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".exe", selected.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sha256Checksum_IsNeverSelectedAsProgramAsset()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.Windows, UpdateArchitecture.X64);

        // Only a checksum file is present for this platform — must not be picked.
        var assets = new[] { new ReleaseAsset("AI_GitHub_Manager_Setup_1.6.2_win-x64.exe.sha256", "https://example.invalid/x.sha256") };

        var selected = ReleaseAssetSelector.SelectFor(platform, assets);

        Assert.Null(selected);
    }

    [Fact]
    public void MultipleSimilarAssets_PicksExactPlatformMatchOnly()
    {
        var assets = new[]
        {
            new ReleaseAsset("AI-GitHub-Manager-v1.6.2-macos-x64.zip", "https://example.invalid/mac-x64.zip"),
            new ReleaseAsset("AI-GitHub-Manager-v1.6.2-macos-arm64.zip", "https://example.invalid/mac-arm64.zip"),
        };
        var platform = new PlatformDescriptor(UpdateOperatingSystem.MacOS, UpdateArchitecture.Arm64);

        var selected = ReleaseAssetSelector.SelectFor(platform, assets);

        Assert.NotNull(selected);
        Assert.Equal("AI-GitHub-Manager-v1.6.2-macos-arm64.zip", selected!.Name);
    }

    [Fact]
    public void UnknownOperatingSystem_ReturnsNull()
    {
        var platform = new PlatformDescriptor(UpdateOperatingSystem.Unknown, UpdateArchitecture.X64);
        var selected = ReleaseAssetSelector.SelectFor(platform, TypicalReleaseAssets);

        Assert.Null(selected);
    }
}
