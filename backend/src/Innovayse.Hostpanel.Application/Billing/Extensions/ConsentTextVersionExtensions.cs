using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one place a <see cref="ConsentTextVersion"/> meets its wire value, the text the portal sends and a consent
/// stores. The values are the ones the portal's own enum carries (<c>client/types/consenttextversion.ts</c>), so a
/// version the portal never published cannot be recorded as evidence. Every member has its row in one table; a test
/// walks the enum so a member added without a row is caught.
/// </summary>
public static class ConsentTextVersionExtensions
{
    /// <summary>The wire value of every published version.</summary>
    private static readonly Dictionary<ConsentTextVersion, string> Wire = new()
    {
        [ConsentTextVersion.AccountCard] = "account-card-2026-11",
        [ConsentTextVersion.CheckoutAgreement] = "checkout-agreement-2026-11-b",
    };

    /// <summary>Whether a text is the wire value of a published wording version, compared exactly.</summary>
    /// <param name="value">The text a request carried.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is the wire value of one of the versions.</returns>
    public static bool IsPublishedConsentTextVersion(this string? value) =>
        value is not null && Wire.ContainsValue(value);
}
