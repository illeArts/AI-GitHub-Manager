using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AI.GitHubManager.App.Services;

namespace AI.GitHubManager.App.Views;

public partial class AboutWindow : Window
{
    private const string GitHubProjectUrl = "https://github.com/illeArts/AI-GitHub-Manager";

    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build ?? 0}";

        // Build = full FileVersion (e.g. 1.6.3.0), independent of the display version above.
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                           ?? version?.ToString() ?? "unknown";
        BuildValue.Text = fileVersion;

        var commitHash = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefaultValue("GitCommitHash") ?? "unknown";
        CommitValue.Text = commitHash;

        OsValue.Text = RuntimeInformation.OSDescription;
        ArchValue.Text = RuntimeInformation.ProcessArchitecture.ToString();
        DotnetValue.Text = RuntimeInformation.FrameworkDescription;

        ApplyStrings();
        L.Changed += ApplyStrings;
        Closed += (_, _) => L.Changed -= ApplyStrings;
    }

    private void ApplyStrings()
    {
        var s = LocalizedStrings.Instance;

        Title = L.T("Über AI GitHub Manager", "About AI GitHub Manager");
        TitleBarText.Text = s.AboutTitle;
        DescriptionText.Text = s.AboutDescription;

        BuildLabel.Text  = s.AboutBuildLabel;
        CommitLabel.Text = s.AboutCommitLabel;
        OsLabel.Text     = s.AboutOsLabel;
        ArchLabel.Text   = s.AboutArchLabel;
        DotnetLabel.Text = s.AboutDotnetLabel;

        LicenseLabel.Text = s.AboutLicenseLabel;
        LicenseValue.Text = s.AboutLicenseValue;

        GitHubLinkButton.Content = s.AboutGitHubLinkLabel;
        PrivacyText.Text = s.AboutPrivacyNotice;
        NotGitHubProductText.Text = s.AboutNotGitHubProduct;
        CopyrightText.Text = s.AboutCopyright;
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    // Opens the GitHub project page via the system's default browser
    // (UseShellExecute = true — the same safe pattern used elsewhere in the
    // app for external links). Never shells out to a raw command string.
    private void OnGitHubLinkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubProjectUrl) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort only — no browser available or blocked by the OS.
            // Not fatal for the About dialog.
        }
    }

    // Allow dragging the custom title bar
    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}

internal static class AssemblyMetadataExtensions
{
    public static string? FirstOrDefaultValue(this IEnumerable<AssemblyMetadataAttribute> attributes, string key)
    {
        foreach (var attr in attributes)
        {
            if (attr.Key == key) return attr.Value;
        }
        return null;
    }
}
