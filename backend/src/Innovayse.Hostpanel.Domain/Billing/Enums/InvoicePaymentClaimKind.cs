namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>Who is paying an invoice by card while a <see cref="InvoicePaymentClaim"/> is held.</summary>
public enum InvoicePaymentClaimKind
{
    /// <summary>The renewal job, charging a saved card with nobody present.</summary>
    Automatic = 0,

    /// <summary>The client, paying with a saved card from the portal.</summary>
    Manual = 1,
}
