namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>How the automatic charge of a renewal invoice ended.</summary>
public enum RenewalChargeOutcome
{
    /// <summary>Nothing was charged and nothing is wrong: the invoice, the account or the card is not eligible.</summary>
    Skipped = 0,

    /// <summary>The card was charged and the invoice settled.</summary>
    Charged = 1,

    /// <summary>The cardholder must authenticate (3-D Secure); the invoice stays unpaid and the client was told.</summary>
    NeedsAuthentication = 2,

    /// <summary>The card was declined; the invoice stays unpaid and the client was told.</summary>
    Declined = 3,

    /// <summary>The gateway could not be reached; what happened is reconciled before any new attempt.</summary>
    Unavailable = 4,

    /// <summary>Another payment attempt holds the invoice; this run leaves it alone.</summary>
    Busy = 5,
}
