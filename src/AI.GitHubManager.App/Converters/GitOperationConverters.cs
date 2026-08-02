using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Operations;

namespace AI.GitHubManager.App.Converters;

/// <summary>
/// Converts a <see cref="GitOperationDefinition"/> to its title in the
/// current UI language (Teil B3). The dropdown's ItemsSource is re-raised
/// via PropertyChanged whenever the language switches (see
/// MainWindowViewModel.RaiseSelectedOperationPropertiesChanged), which
/// makes Avalonia re-evaluate this converter for every item — that's what
/// keeps the dropdown itself, not just the explanation panel below it,
/// in sync with the selected language.
/// </summary>
public sealed class GitOperationTitleConverter : IValueConverter
{
    public static readonly GitOperationTitleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GitOperationDefinition op ? op.Title(L.IsEnglish) : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a <see cref="GitOperationDefinition"/> to its description in the current UI language.</summary>
public sealed class GitOperationDescriptionConverter : IValueConverter
{
    public static readonly GitOperationDescriptionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GitOperationDefinition op ? op.Description(L.IsEnglish) : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a <see cref="GitOperationDefinition"/> to "Title — risk level" for the advanced list.</summary>
public sealed class GitOperationTitleWithRiskConverter : IValueConverter
{
    public static readonly GitOperationTitleWithRiskConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not GitOperationDefinition op) return value?.ToString();
        var risk = MainWindowRiskText(op.RiskLevel);
        return $"{op.Title(L.IsEnglish)} — {risk}";
    }

    private static string MainWindowRiskText(GitOperationRiskLevel level) => level switch
    {
        GitOperationRiskLevel.Safe      => L.T("Sicher",     "Safe"),
        GitOperationRiskLevel.Caution   => L.T("Vorsicht",   "Caution"),
        GitOperationRiskLevel.Advanced  => L.T("Erweitert",  "Advanced"),
        GitOperationRiskLevel.Dangerous => L.T("Gefährlich", "Dangerous"),
        _ => L.T("Unbekannt", "Unknown"),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
