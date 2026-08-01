using System.Runtime.InteropServices;

namespace AI.GitHubManager.Core.Update;

/// <summary>Coarse operating system classification used for update asset matching.</summary>
public enum UpdateOperatingSystem
{
    Unknown = 0,
    Windows,
    MacOS,
    Linux,
}

/// <summary>Coarse CPU architecture classification used for update asset matching.</summary>
public enum UpdateArchitecture
{
    Unknown = 0,
    X64,
    Arm64,
}

/// <summary>
/// Describes the platform AI GitHub Manager is currently running on, using the
/// real .NET platform/architecture APIs (never string sniffing of file names).
/// </summary>
public sealed record PlatformDescriptor(UpdateOperatingSystem OperatingSystem, UpdateArchitecture Architecture)
{
    /// <summary>Detects the current process' operating system and architecture.</summary>
    public static PlatformDescriptor Current => new(DetectOperatingSystem(), DetectArchitecture());

    private static UpdateOperatingSystem DetectOperatingSystem()
    {
        if (System.OperatingSystem.IsWindows()) return UpdateOperatingSystem.Windows;
        if (System.OperatingSystem.IsMacOS()) return UpdateOperatingSystem.MacOS;
        if (System.OperatingSystem.IsLinux()) return UpdateOperatingSystem.Linux;
        return UpdateOperatingSystem.Unknown;
    }

    private static UpdateArchitecture DetectArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.X64 => UpdateArchitecture.X64,
        System.Runtime.InteropServices.Architecture.Arm64 => UpdateArchitecture.Arm64,
        _ => UpdateArchitecture.Unknown,
    };

    public override string ToString() => $"{OperatingSystem}/{Architecture}";
}
