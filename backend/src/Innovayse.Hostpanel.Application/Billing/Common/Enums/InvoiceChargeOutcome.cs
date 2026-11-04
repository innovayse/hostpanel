namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>How charging an invoice's saved method ended.</summary>
public enum InvoiceChargeOutcome
{
    /// <summary>The method was charged and the invoice settled; the claim is ended.</summary>
    Settled = 0,

    /// <summary>The gateway declined; nothing was taken and the claim is ended.</summary>
    Declined = 1,

    /// <summary>The payer must authenticate; nothing was taken and the claim is ended.</summary>
    ActionRequired = 2,

    /// <summary>The gateway accepted the charge but has not settled it; the claim stays held and is looked up later.</summary>
    Pending = 3,

    /// <summary>The gateway could not be reached or did not answer, so it is not known whether money moved; the claim stays held.</summary>
    Unavailable = 4,

    /// <summary>The gateway took the money but it could not be recorded against the invoice; the claim stays held unless the ledger shows the money handed back.</summary>
    NotRecorded = 5,
}
