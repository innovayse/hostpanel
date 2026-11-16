namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>Whether a quote's validity date is still ahead, as the quote lists filter on it. Travels on the wire by name.</summary>
public enum QuoteValidity
{
    /// <summary>The quote's expiry date has not passed yet.</summary>
    Valid = 0,

    /// <summary>The quote's expiry date has passed.</summary>
    Expired = 1,
}
