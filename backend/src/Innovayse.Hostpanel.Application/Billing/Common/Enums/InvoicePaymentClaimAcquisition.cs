namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>What asking for the right to charge an invoice's card came to.</summary>
public enum InvoicePaymentClaimAcquisition
{
    /// <summary>The right is held: the caller may charge.</summary>
    Acquired = 0,

    /// <summary>Another payment attempt holds the invoice, or what it did is not known yet: the caller must not charge.</summary>
    Busy = 1,

    /// <summary>A stale attempt had already taken the money; it was settled just now and there is nothing left to charge.</summary>
    AlreadySettled = 2,
}
