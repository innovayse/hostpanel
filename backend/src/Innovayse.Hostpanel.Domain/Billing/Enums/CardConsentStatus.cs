namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>Where a <see cref="CardChargeConsent"/> stands.</summary>
public enum CardConsentStatus
{
    /// <summary>Asked for, but the gateway has not yet confirmed the card was saved, so nothing may be charged.</summary>
    Pending = 0,

    /// <summary>The card was saved with the client's agreement: automatic renewal charges may use it.</summary>
    Active = 1,

    /// <summary>The client withdrew the agreement, or the card was removed: the card is never charged automatically.</summary>
    Revoked = 2,
}
