namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A payment transaction record for an invoice or client account.
/// Records all monetary movements (credit, debit).
/// Stored in the <c>transactions</c> table.
/// </summary>
public sealed class Transaction : AggregateRoot
{
    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the FK to the associated invoice; null for account-level transactions.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the transaction type (Credit or Debit).</summary>
    public string Type { get; private set; } = null!;

    /// <summary>Gets the transaction amount (always positive; use Type to determine direction).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the transaction fees (e.g. Stripe, PayPal fees).</summary>
    public decimal Fees { get; private set; }

    /// <summary>Gets the currency code (e.g. USD, EUR).</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Gets the payment gateway name (e.g. Stripe, PayPal, or null for manual).</summary>
    public string? Gateway { get; private set; }

    /// <summary>Gets the external transaction ID from the gateway; null for manual transactions.</summary>
    public string? TransactionId { get; private set; }

    /// <summary>Gets the human-readable description (e.g. "Invoice #123 Payment").</summary>
    public string Description { get; private set; } = null!;

    /// <summary>Gets the UTC timestamp when the transaction occurred.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Transaction() : base(0) { }

    /// <summary>
    /// Creates a new transaction record.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="invoiceId">Optional FK to the invoice.</param>
    /// <param name="type">Credit or Debit.</param>
    /// <param name="amount">Transaction amount (positive).</param>
    /// <param name="fees">Transaction fees (e.g. gateway fees).</param>
    /// <param name="currency">Currency code.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="gateway">Optional gateway name.</param>
    /// <param name="transactionId">Optional external transaction ID.</param>
    /// <returns>A new transaction aggregate.</returns>
    public static Transaction Create(
        int clientId,
        int? invoiceId,
        string type,
        decimal amount,
        decimal fees,
        string currency,
        string description,
        string? gateway = null,
        string? transactionId = null)
    {
        return new Transaction
        {
            ClientId = clientId,
            InvoiceId = invoiceId,
            Type = type,
            Amount = amount,
            Fees = fees,
            Currency = currency,
            Description = description,
            Gateway = gateway,
            TransactionId = transactionId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Updates an existing transaction record.
    /// </summary>
    public void Update(
        int clientId,
        string type,
        decimal amount,
        decimal fees,
        string currency,
        string description,
        string? gateway = null,
        string? transactionId = null)
    {
        ClientId = clientId;
        Type = type;
        Amount = amount;
        Fees = fees;
        Currency = currency;
        Description = description;
        Gateway = gateway;
        TransactionId = transactionId;
    }
}
