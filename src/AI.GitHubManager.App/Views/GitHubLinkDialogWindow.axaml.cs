using Avalonia.Controls;
using Avalonia.Input;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Core.Remote;

namespace AI.GitHubManager.App.Views;

/// <summary>Result of the manual GitHub-link dialog — always derived from a
/// validated, user-typed URL (or "owner/repo" shorthand), never fabricated
/// from the logged-in GitHub CLI account.</summary>
public sealed record GitHubLinkDialogResult(string Owner, string Repository, string WebUrl);

/// <summary>
/// "GitHub-Link festlegen …" / "GitHub-Link bearbeiten …" — manual entry dialog
/// used when a project has no (parseable) git "origin" remote. Validates the
/// input against <see cref="RemoteUrlNormalizer.ParseManualEntry"/> before the
/// Save button is enabled; shows a clear German error message otherwise.
/// </summary>
public partial class GitHubLinkDialogWindow : Window
{
    private readonly bool _isEdit;

    public GitHubLinkDialogWindow() : this(new ManagedProject()) { }

    public GitHubLinkDialogWindow(ManagedProject project)
    {
        _isEdit = project.HasGitHubLink;
        InitializeComponent();

        Title = L.T("GitHub-Link", "GitHub link");
        TitleText.Text = _isEdit
            ? L.T("GitHub-Link bearbeiten", "Edit GitHub link")
            : L.T("GitHub-Link festlegen", "Set GitHub link");
        HintText.Text = L.T(
            "Kein Git-Remote gefunden oder Remote nicht eindeutig. Bitte den echten GitHub-Link eingeben " +
            "(z. B. https://github.com/owner/repo oder owner/repo).",
            "No git remote found, or the remote could not be parsed. Please enter the real GitHub link " +
            "(e.g. https://github.com/owner/repo or owner/repo).");
        CancelButton.Content = L.T("Abbrechen", "Cancel");
        SaveButton.Content = L.T("Speichern", "Save");

        UrlBox.Text = project.HasGitHubLink ? project.RepositoryWebUrl : string.Empty;

        KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; TrySave(); } };
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(null);

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TrySave();

    private void TrySave()
    {
        var info = RemoteUrlNormalizer.ParseManualEntry(UrlBox.Text);
        if (info is null)
        {
            ErrorText.Text = L.T(
                "Ungültiger GitHub-Link. Bitte eine vollständige URL (https://github.com/owner/repo) " +
                "oder \"owner/repo\" eingeben.",
                "Invalid GitHub link. Please enter a full URL (https://github.com/owner/repo) or \"owner/repo\".");
            ErrorText.IsVisible = true;
            return;
        }

        Close(new GitHubLinkDialogResult(info.Owner, info.Repository, info.WebUrl));
    }
}
