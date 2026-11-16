namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The registered address of a VAT-registered business, as the UK tax authority returns it.</summary>
/// <param name="Line1">First address line.</param>
/// <param name="Line2">Second address line.</param>
/// <param name="Line3">Third address line.</param>
/// <param name="Line4">Fourth address line.</param>
/// <param name="Line5">Fifth address line.</param>
/// <param name="Postcode">The postcode.</param>
/// <param name="CountryCode">The country code.</param>
internal sealed record HmrcAddressWire(
    string? Line1, string? Line2, string? Line3, string? Line4, string? Line5, string? Postcode, string? CountryCode)
{
    /// <summary>Joins the parts that are present into one line.</summary>
    /// <returns>The address, or <see langword="null"/> when no part is present.</returns>
    public string? ToSingleLine()
    {
        var parts = new[] { Line1, Line2, Line3, Line4, Line5, Postcode, CountryCode }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
}
