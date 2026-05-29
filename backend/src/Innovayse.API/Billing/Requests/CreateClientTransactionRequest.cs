namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/transactions — creates a new client transaction.</summary>
public sealed class CreateClientTransactionRequest
{
    /// <summary>Gets or initialises the client ID.</summary>
    public required int ClientId { get; init; }

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

    /// <summary>Gets or initialises whether to adjust the client's credit balance.</summary>
    public bool AddToCredit { get; init; }
}
