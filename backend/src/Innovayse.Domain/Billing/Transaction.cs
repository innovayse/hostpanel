namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a financial transaction on a client's account.
/// Tracks money in (payments), money out (refunds/withdrawals), fees, and credit adjustments.
/// Stored in the <c>transactions</c> table.
/// </summary>
public sealed class Transaction : Entity
{
    /// <summary>Gets the FK to the owning <see cref="Clients.Client"/>.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the UTC timestamp of the transaction.</summary>
    public DateTimeOffset Date { get; private set; }

    /// <summary>Gets the human-readable description of the transaction.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the external transaction reference identifier.</summary>
    public string TransactionId { get; private set; } = string.Empty;

    /// <summary>Gets the optional FK to the related <see cref="Invoice"/>, or <see langword="null"/>.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the payment method used (e.g. "Stripe", "Manual", "Refund").</summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>Gets the amount credited to the client's account.</summary>
    public decimal AmountIn { get; private set; }

    /// <summary>Gets the amount debited from the client's account.</summary>
    public decimal AmountOut { get; private set; }

    /// <summary>Gets the transaction fees charged.</summary>
    public decimal Fees { get; private set; }

    /// <summary>Gets whether this transaction was added to or deducted from the client's credit balance.</summary>
    public bool AddedToCredit { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Transaction() : base(0) { }

    /// <summary>
    /// Creates a new transaction with the specified details.
    /// </summary>
    /// <param name="clientId">FK to the owning client.</param>
    /// <param name="date">UTC timestamp of the transaction.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="transactionId">External transaction reference.</param>
    /// <param name="invoiceId">Optional related invoice ID.</param>
    /// <param name="paymentMethod">Payment method used.</param>
    /// <param name="amountIn">Amount credited (≥ 0).</param>
    /// <param name="amountOut">Amount debited (≥ 0).</param>
    /// <param name="fees">Transaction fees (≥ 0).</param>
    /// <param name="addedToCredit">Whether this affects the client's credit balance.</param>
    /// <returns>A new <see cref="Transaction"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="amountIn"/> and <paramref name="amountOut"/> are 0.</exception>
    public static Transaction Create(
        int clientId,
        DateTimeOffset date,
        string description,
        string transactionId,
        int? invoiceId,
        string paymentMethod,
        decimal amountIn,
        decimal amountOut,
        decimal fees,
        bool addedToCredit)
    {
        if (amountIn <= 0 && amountOut <= 0)
        {
            throw new ArgumentException("At least one of AmountIn or AmountOut must be greater than 0.");
        }

        return new Transaction
        {
            ClientId = clientId,
            Date = date,
            Description = description,
            TransactionId = transactionId,
            InvoiceId = invoiceId,
            PaymentMethod = paymentMethod,
            AmountIn = amountIn,
            AmountOut = amountOut,
            Fees = fees,
            AddedToCredit = addedToCredit,
        };
    }
}
