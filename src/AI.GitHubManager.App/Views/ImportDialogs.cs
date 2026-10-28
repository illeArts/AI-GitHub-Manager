using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Projects;

namespace AI.GitHubManager.App.Views;

internal static class ImportDialogs
{
    private static string Text(string key) => LocalizedStrings.Import(key);

    public static async Task<string?> Target(Window owner, string remote, string initial)
    {
        var dialog = CreateWindow();
        var path = new TextBox { Text = initial, Watermark = Text("Pick") };
        Avalonia.Automation.AutomationProperties.SetName(path, Text("Pick"));
        var browse = new Button { Content = Text("ChooseOther") };
        browse.Click += async (_, _) =>
        {
            var folders = await dialog.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                { Title = Text("Pick"), AllowMultiple = false });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } selected) path.Text = selected;
        };
        var proceed = new Button { Content = Text("Continue") };
        proceed.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(path.Text)) return;
            var confirmed = await Choice(dialog, string.Format(Text("Confirm"), remote, path.Text), ["Continue", "Cancel"]);
            if (confirmed == "Continue") dialog.Close(path.Text);
        };
        var cancel = new Button { Content = Text("Cancel"), IsCancel = true };
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 12, Children =
            { new TextBlock { Text = Text("Pick"), TextWrapping = Avalonia.Media.TextWrapping.Wrap }, path, browse,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { proceed, cancel } } } };
        return await dialog.ShowDialog<string?>(owner);
    }

    public static async Task<ImportChoice> Existing(Window owner, ImportInspection state)
    {
        var details = string.Format(Text("ExistingDetails"), state.Path, state.Branch, state.Upstream,
            Text(state.Dirty ? "Yes" : "No"), state.Ahead, state.Behind, Text(state.OperationInProgress ? "Yes" : "No"));
        var choice = await Choice(owner, details, state.CanUpdate ? ["Link", "Update", "Cancel"] : ["Link", "Cancel"]);
        return choice switch { "Link" => ImportChoice.Link, "Update" => ImportChoice.Update, _ => ImportChoice.Cancel };
    }

    public static async Task<bool> Error(Window owner, string message)
        => await Choice(owner, message, ["ChooseOther", "Cancel"]) == "ChooseOther";

    private static async Task<string?> Choice(Window owner, string message, string[] options)
    {
        var dialog = CreateWindow();
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var key in options)
        {
            var button = new Button { Content = Text(key), IsCancel = key == "Cancel", Margin = new Thickness(0, 0, 8, 8) };
            button.Click += (_, _) => dialog.Close(key);
            buttons.Children.Add(button);
        }
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children =
            { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons } };
        return await dialog.ShowDialog<string?>(owner);
    }

    private static Window CreateWindow() => new()
    {
        Title = Text("Import"), Width = 660, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
    };
}
