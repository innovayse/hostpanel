using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// The rule a product SKU must meet. A SKU is the stable machine identifier a fulfilment plugin
/// keys on, so it is normalised (trimmed, upper case) before it is checked or stored, and only
/// upper-case letters and digits joined by single hyphens, underscores or dots are accepted.
/// </summary>
public static partial class ProductSku
{
    /// <summary>The longest SKU accepted, after normalisation.</summary>
    public const int MaxLength = 64;

    /// <summary>The accepted shape: upper-case alphanumeric words joined by a single <c>-</c>, <c>_</c> or <c>.</c>.</summary>
    public const string Pattern = "^[A-Z0-9]+(?:[-_.][A-Z0-9]+)*$";

    /// <summary>Brings a SKU to its stored form: surrounding white space removed, upper case.</summary>
    /// <param name="sku">The SKU as typed or imported, or <see langword="null"/>.</param>
    /// <returns>The normalised SKU, or <see langword="null"/> when <paramref name="sku"/> is null or only white space.</returns>
    public static string? Normalize(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        return sku.Trim().ToUpperInvariant();
    }

    /// <summary>Whether <paramref name="sku"/>, once normalised, is an acceptable SKU.</summary>
    /// <param name="sku">The candidate SKU, in any case and with any surrounding white space.</param>
    /// <returns>
    /// <see langword="true"/> when the normalised SKU is non-empty, at most <see cref="MaxLength"/>
    /// characters and matches <see cref="Pattern"/>.
    /// </returns>
    public static bool IsValid(string? sku)
    {
        var normalized = Normalize(sku);
        return normalized is not null && normalized.Length <= MaxLength && PatternRegex().IsMatch(normalized);
    }

    /// <summary>The compiled <see cref="Pattern"/>.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex PatternRegex();
}
