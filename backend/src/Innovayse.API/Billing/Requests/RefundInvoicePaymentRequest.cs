namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/billing/{id}/refund — refunds a paid invoice.</summary>
public sealed class RefundInvoicePaymentRequest
{
    /// <summary>Gets or initialises the refund amount (0 = full refund).</summary>
    public required decimal Amount { get; init; }

    /// <summary>Gets or initialises the refund type: Gateway, Manual, or CreditBalance.</summary>
    public required string RefundType { get; init; }

    /// <summary>Gets or initialises the payment gateway name.</summary>
    public required string Gateway { get; init; }

    /// <summary>Gets or initialises the original payment transaction ID being refunded.</summary>
    public required string TransactionId { get; init; }

    /// <summary>Gets or initialises the external refund transaction reference (required for Manual type).</summary>
    public string? RefundTransactionId { get; init; }

    /// <summary>Gets or initialises optional refund notes.</summary>
    public string? Notes { get; init; }
}
