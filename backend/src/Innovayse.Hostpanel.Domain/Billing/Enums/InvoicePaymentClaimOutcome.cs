namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>How a <see cref="InvoicePaymentClaim"/> ended.</summary>
public enum InvoicePaymentClaimOutcome
{
    /// <summary>The charge was taken and the invoice settled.</summary>
    Settled = 0,

    /// <summary>The attempt took no money (a decline, a refusal, or an authentication the cardholder has to do).</summary>
    Failed = 1,

    /// <summary>The attempt never reported back, the gateway had taken no money for it, and its time ran out.</summary>
    Expired = 2,
}
