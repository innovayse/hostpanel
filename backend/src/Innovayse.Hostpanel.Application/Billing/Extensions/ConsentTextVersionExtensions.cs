using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one place a <see cref="ConsentTextVersion"/> meets its wire value, the text the portal sends and a consent
/// stores. The values are the ones the portal's own enum carries (<c>client/types/consenttextversion.ts</c>), so a
/// version the portal never published cannot be recorded as evidence.
/// </summary>
public static class ConsentTextVersionExtensions
{
    /// <summary>The wire value of <see cref="ConsentTextVersion.AccountCard"/>.</summary>
    private const string AccountCardWire = "account-card-2026-11";

    /// <summary>The wire value of <see cref="ConsentTextVersion.CheckoutAgreement"/>.</summary>
    private const string CheckoutAgreementWire = "checkout-agreement-2026-11-b";

    /// <summary>Gets the wire value of a published wording version.</summary>
    /// <param name="version">The version.</param>
    /// <returns>The text the portal sends and a consent stores for it.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a defined version.</exception>
    public static string ToWire(this ConsentTextVersion version) => version switch
    {
        ConsentTextVersion.AccountCard => AccountCardWire,
        ConsentTextVersion.CheckoutAgreement => CheckoutAgreementWire,
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, "The consent text version is not defined."),
    };

    /// <summary>Whether a text is the wire value of a published wording version, compared exactly.</summary>
    /// <param name="value">The text a request carried.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is the wire value of one of the versions.</returns>
    public static bool IsPublishedConsentTextVersion(this string? value) =>
        Enum.GetValues<ConsentTextVersion>().Any(version => string.Equals(version.ToWire(), value, StringComparison.Ordinal));
}
