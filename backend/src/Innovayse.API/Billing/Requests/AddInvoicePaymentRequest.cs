namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/billing/{id}/payments — records a manual payment.</summary>
public sealed class AddInvoicePaymentRequest
{
    /// <summary>Gets or initialises the UTC timestamp of the payment.</summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>Gets or initialises the payment gateway name.</summary>
    public required string Gateway { get; init; }

    /// <summary>Gets or initialises the external transaction reference.</summary>
    public required string TransactionId { get; init; }

    /// <summary>Gets or initialises the payment amount.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Gets or initialises transaction fees charged by the gateway.</summary>
    public decimal Fees { get; init; }

    /// <summary>Gets or initialises optional payment notes.</summary>
    public string? Notes { get; init; }
}
