namespace AI.GitHubManager.Core.Export;

public static class ExportProfiles
{
    private static readonly string[] MacMetadata = [".DS_Store", "**/.DS_Store", "__MACOSX/**", "**/__MACOSX/**", "._*", "**/._*", ".Trashes/**", ".fseventsd/**"];
    private static readonly string[] Build = ["**/bin/**", "**/obj/**", "**/build/**", "**/dist/**", "**/DerivedData/**", "**/.build/**", "**/node_modules/**", "**/packages/**", "**/TestResults/**", "**/coverage/**"];
    private static readonly string[] IdeAndTemp = ["**/.vs/**", "**/.idea/**", "**/xcuserdata/**", "**/*.xcuserstate", "**/*.user", "**/*.suo", "**/*.tmp", "**/*.temp", "**/*.bak", "**/*.swp", "**/*~", "**/.cache/**"];
    private static readonly string[] Binaries = ["**/*.zip", "**/*.dmg", "**/*.iso", "**/*.mp4", "**/*.mov", "**/*.dll", "**/*.exe"];

    public static IReadOnlyList<ExportProfile> All { get; } =
    [
        new(ExportProfileKind.WindowsExchange, "Windows-Austausch", "Entfernt nur typische macOS-Metadaten.", MacMetadata),
        new(ExportProfileKind.CleanSource, "Sauberer Quellcode", "Quellcode ohne Git-Verlauf, Builds, Caches und Benutzerdaten.", MacMetadata.Concat([".git", ".git/**", "**/.git", "**/.git/**"]).Concat(Build).Concat(IdeAndTemp).ToArray()),
        new(ExportProfileKind.AiAnalysis, "KI-Analyse", "Kompakter Quellcodeexport für Analysewerkzeuge.", MacMetadata.Concat([".git", ".git/**", "**/.git", "**/.git/**"]).Concat(Build).Concat(IdeAndTemp).Concat(Binaries).ToArray()),
        new(ExportProfileKind.CompleteFileArchive, "Vollständiges Dateiarchiv", "Dateiarchiv; kein Git-Wiederherstellungsbackup.", MacMetadata)
    ];

    public static ExportProfile Get(ExportProfileKind kind) => All.Single(x => x.Kind == kind);
}
