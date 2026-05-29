namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/billing/{id}/credit — applies a credit to an invoice.</summary>
public sealed class ApplyInvoiceCreditRequest
{
    /// <summary>Gets or initialises the credit amount to apply.</summary>
    public required decimal Amount { get; init; }
}
