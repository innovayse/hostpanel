using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>
/// The one place a list of reasons is written as the text a column stores and read back. The text is the reason names between commas
/// with a comma at both ends (<c>,BlockedIp,DisposableEmail,</c>), so a filter for one reason is a plain substring test for the name
/// between two commas that never matches half of another name.
/// </summary>
public static class FraudReasonText
{
    /// <summary>Longest text the columns hold; every reason name fits several times over.</summary>
    public const int MaxLength = 300;

    /// <summary>Separates the names, and starts and ends a non-empty text.</summary>
    private const char Separator = ',';

    /// <summary>Writes a list of reasons as stored.</summary>
    /// <param name="reasons">The reasons; a repeat is written once.</param>
    /// <returns>The stored text; empty for no reason.</returns>
    public static string Format(IEnumerable<FraudReasonCode> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        var names = reasons.Distinct().Select(r => r.ToString()).ToList();
        return names.Count == 0 ? string.Empty : $"{Separator}{string.Join(Separator, names)}{Separator}";
    }

    /// <summary>Reads the stored text back.</summary>
    /// <param name="stored">The stored text.</param>
    /// <returns>The reasons in the order they were written.</returns>
    public static IReadOnlyList<FraudReasonCode> Parse(string? stored)
    {
        return (stored ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(Enum.Parse<FraudReasonCode>)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>The substring a stored text holds when it names the reason, for filtering a column.</summary>
    /// <param name="reason">The reason to look for.</param>
    /// <returns>The reason name between two commas.</returns>
    public static string Marker(FraudReasonCode reason)
    {
        return $"{Separator}{reason}{Separator}";
    }
}
