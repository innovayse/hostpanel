namespace Innovayse.API.Billing.Requests;

/// <summary>Request to create a transaction.</summary>
public sealed class CreateTransactionRequest
{
    /// <summary>Gets or sets the client ID.</summary>
    public int ClientId { get; set; }

    /// <summary>Gets or sets the transaction type (Credit or Debit).</summary>
    public string Type { get; set; } = null!;

    /// <summary>Gets or sets the transaction amount (always positive).</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the transaction fees.</summary>
    public decimal Fees { get; set; }

    /// <summary>Gets or sets the currency code (e.g. USD).</summary>
    public string Currency { get; set; } = null!;

    /// <summary>Gets or sets the human-readable description.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Gets or sets the optional payment gateway name (e.g. Stripe).</summary>
    public string? Gateway { get; set; }

    /// <summary>Gets or sets the optional external transaction ID.</summary>
    public string? TransactionId { get; set; }
}
