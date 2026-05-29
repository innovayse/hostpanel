namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A financial transaction recorded against an <see cref="Invoice"/>.
/// Represents payments, refunds, or credits. Owned by the Invoice aggregate.
/// Stored in the <c>invoice_transactions</c> table.
/// </summary>
public sealed class InvoiceTransaction : Entity
{
    /// <summary>Gets the FK to the parent <see cref="Invoice"/>.</summary>
    public int InvoiceId { get; private set; }

    /// <summary>Gets the UTC timestamp of the transaction.</summary>
    public DateTimeOffset Date { get; private set; }

    /// <summary>Gets the payment gateway name (e.g. "Stripe", "Manual").</summary>
    public string Gateway { get; private set; } = string.Empty;

    /// <summary>Gets the external transaction reference from the payment gateway.</summary>
    public string TransactionId { get; private set; } = string.Empty;

    /// <summary>Gets the transaction amount in the invoice currency.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the type of this transaction.</summary>
    public InvoiceTransactionType Type { get; private set; }

    /// <summary>Gets the transaction fees charged by the gateway.</summary>
    public decimal Fees { get; private set; }

    /// <summary>Gets optional notes for this transaction; null when not provided.</summary>
    public string? Notes { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private InvoiceTransaction() : base(0) { }

    /// <summary>
    /// Creates a new invoice transaction with the specified details.
    /// </summary>
    /// <param name="invoiceId">FK to the parent invoice.</param>
    /// <param name="date">UTC timestamp of the transaction.</param>
    /// <param name="gateway">Payment gateway name.</param>
    /// <param name="transactionId">External transaction reference.</param>
    /// <param name="amount">Transaction amount (must be greater than 0).</param>
    /// <param name="type">The type of transaction.</param>
    /// <param name="fees">Transaction fees charged by the gateway (≥ 0).</param>
    /// <param name="notes">Optional notes.</param>
    /// <returns>A new <see cref="InvoiceTransaction"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not greater than 0.</exception>
    public static InvoiceTransaction Create(
        int invoiceId,
        DateTimeOffset date,
        string gateway,
        string transactionId,
        decimal amount,
        InvoiceTransactionType type,
        decimal fees = 0m,
        string? notes = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount must be greater than 0.");
        }

        return new InvoiceTransaction
        {
            InvoiceId = invoiceId,
            Date = date,
            Gateway = gateway,
            TransactionId = transactionId,
            Amount = amount,
            Type = type,
            Fees = fees,
            Notes = notes,
        };
    }
}
