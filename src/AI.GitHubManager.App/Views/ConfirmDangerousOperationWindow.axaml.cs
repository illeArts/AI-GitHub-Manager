using Avalonia.Controls;
using Avalonia.Input;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Operations;

namespace AI.GitHubManager.App.Views;

/// <summary>
/// Confirmation dialog for advanced/dangerous Git operations (Teil B7/D).
/// Design rules enforced here, not just described:
///  - No button is IsDefault, so pressing Enter anywhere in this window
///    never triggers execution.
///  - Confirm starts disabled and only becomes enabled once every required
///    condition is met (understood checkbox; for Dangerous risk level, the
///    exact branch name typed; for ops needing a target ref, a validated
///    ref entered).
///  - Cancel is reachable via Escape (IsCancel) and is the only action that
///    requires no extra input.
///  - Repository, branch, affected scope, and recoverability are always
///    shown explicitly (never assumed known to the user).
/// </summary>
public partial class ConfirmDangerousOperationWindow : Window
{
    private static readonly HashSet<string> IdsNeedingTargetRef = new() { "reset", "hard-reset", "rebase", "cherry-pick" };

    private readonly AdvancedOperationConfirmationRequest _request;

    public ConfirmDangerousOperationWindow() : this(
        new AdvancedOperationConfirmationRequest(GitOperationCatalog.Clean, string.Empty, string.Empty))
    { }

    public ConfirmDangerousOperationWindow(AdvancedOperationConfirmationRequest request)
    {
        _request = request;
        InitializeComponent();
        ApplyContent();

        UnderstoodCheckBox.IsCheckedChanged += (_, _) => RefreshConfirmEnabled();
        TargetRefBox.TextChanged += (_, _) => RefreshConfirmEnabled();
        TypedConfirmationBox.TextChanged += (_, _) => RefreshConfirmEnabled();

        // Belt-and-suspenders: even though no button has IsDefault, explicitly
        // swallow Enter anywhere in the dialog so it can never submit early.
        KeyDown += (_, e) => { if (e.Key == Key.Enter) e.Handled = true; };
    }

    private bool NeedsTargetRef => IdsNeedingTargetRef.Contains(_request.Operation.Id);
    private bool NeedsTypedConfirmation => _request.Operation.RiskLevel == GitOperationRiskLevel.Dangerous;

    private void ApplyContent()
    {
        var op = _request.Operation;

        Title = L.T("Bestätigung erforderlich", "Confirmation required");
        TitleText.Text = L.T($"⚠️ „{op.TitleDe}\" wirklich ausführen?", $"⚠️ Really run \"{op.TitleEn}\"?");

        RepoLabel.Text = L.T("Repository:", "Repository:");
        RepoValue.Text = string.IsNullOrWhiteSpace(_request.RepositoryPath)
            ? L.T("(kein Ordner ausgewählt)", "(no folder selected)")
            : _request.RepositoryPath;

        BranchLabel.Text = L.T("Aktiver Branch:", "Active branch:");
        BranchValue.Text = string.IsNullOrWhiteSpace(_request.Branch)
            ? L.T("(unbekannt)", "(unknown)")
            : _request.Branch;

        ScopeLabel.Text = L.T("Betroffener Umfang:", "Affected scope:");
        ScopeValue.Text = op.WhatChanges(L.IsEnglish);

        RecoverabilityLabel.Text = L.T("Wiederherstellbarkeit:", "Recoverability:");
        RecoverabilityValue.Text = op.Risk(L.IsEnglish);

        CommandLabel.Text = L.T("Technischer Befehl:", "Technical command:");
        CommandValue.Text = op.TechnicalCommand;

        UnderstoodCheckBox.Content = L.T("Ich habe die Risiken verstanden.", "I understand the risks.");

        TargetRefPanel.IsVisible = NeedsTargetRef;
        if (NeedsTargetRef)
            TargetRefLabel.Text = L.T("Ziel (Branch/Commit):", "Target (branch/commit):");

        TypedConfirmationPanel.IsVisible = NeedsTypedConfirmation;
        if (NeedsTypedConfirmation)
        {
            var branchForTyping = string.IsNullOrWhiteSpace(_request.Branch) ? "main" : _request.Branch;
            TypedConfirmationLabel.Text = L.T(
                $"Zum Bestätigen den aktiven Branch-Namen eingeben: \"{branchForTyping}\"",
                $"To confirm, type the active branch name: \"{branchForTyping}\"");
        }

        CancelButton.Content = L.T("Abbrechen", "Cancel");
        ConfirmButton.Content = L.T("Ausführen", "Run");

        RefreshConfirmEnabled();
    }

    private void RefreshConfirmEnabled()
    {
        var ok = UnderstoodCheckBox.IsChecked == true;

        if (NeedsTargetRef)
            ok &= GitRefValidator.IsValidRef(TargetRefBox.Text);

        if (NeedsTypedConfirmation)
        {
            var expected = string.IsNullOrWhiteSpace(_request.Branch) ? "main" : _request.Branch;
            ok &= TypedConfirmationBox.Text?.Trim() == expected;
        }

        ConfirmButton.IsEnabled = ok;
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(new AdvancedOperationConfirmationResult(false, null));

    private void OnConfirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!ConfirmButton.IsEnabled) return; // defensive — should be unreachable
        Close(new AdvancedOperationConfirmationResult(true, NeedsTargetRef ? TargetRefBox.Text?.Trim() : null));
    }
}
