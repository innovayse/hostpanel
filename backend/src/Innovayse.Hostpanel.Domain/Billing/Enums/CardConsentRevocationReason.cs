namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>Why a <see cref="CardChargeConsent"/> was withdrawn.</summary>
public enum CardConsentRevocationReason
{
    /// <summary>The client turned automatic charges off.</summary>
    WithdrawnByClient = 0,

    /// <summary>The saved card the agreement covered was removed.</summary>
    CardRemoved = 1,
}
