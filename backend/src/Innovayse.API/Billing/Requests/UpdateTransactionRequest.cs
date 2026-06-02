namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for PUT /api/transactions/{id} — updates a transaction.</summary>
public sealed class UpdateTransactionRequest
{
    /// <summary>Gets or initialises the transaction date (UTC).</summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>Gets or initialises the human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the external transaction reference.</summary>
    public required string TransactionId { get; init; }

    /// <summary>Gets or initialises the optional related invoice ID.</summary>
    public int? InvoiceId { get; init; }

    /// <summary>Gets or initialises the payment method used.</summary>
    public required string PaymentMethod { get; init; }

    /// <summary>Gets or initialises the amount credited to the account.</summary>
    public required decimal AmountIn { get; init; }

    /// <summary>Gets or initialises the amount debited from the account.</summary>
    public required decimal AmountOut { get; init; }

    /// <summary>Gets or initialises the transaction fees.</summary>
    public decimal Fees { get; init; }
}
