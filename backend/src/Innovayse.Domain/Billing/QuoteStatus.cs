namespace Innovayse.Domain.Billing;

/// <summary>Lifecycle states for a <see cref="Quote"/>.</summary>
public enum QuoteStatus
{
    /// <summary>Quote is saved but not yet sent to the client.</summary>
    Draft,

    /// <summary>Quote has been sent to the client awaiting response.</summary>
    Sent,

    /// <summary>Client has accepted the quote and it can be converted to an invoice.</summary>
    Accepted,

    /// <summary>Client has declined the quote.</summary>
    Declined,

    /// <summary>Quote expiry date has passed and it is no longer valid.</summary>
    Expired,

    /// <summary>Quote has been cancelled and will not be pursued.</summary>
    Cancelled,
}
