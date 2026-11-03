using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// Represents a financial transaction on a client's account.
/// Tracks money in (payments), money out (refunds/withdrawals), fees, and credit adjustments.
/// Stored in the <c>Transactions</c> table.
/// </summary>
public sealed class Transaction : BaseEntity
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

    /// <summary>Gets the payment method used (e.g. a card gateway name, "Manual", "Refund").</summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>Gets the amount credited to the client's account.</summary>
    public decimal AmountIn { get; private set; }

    /// <summary>Gets the amount debited from the client's account.</summary>
    public decimal AmountOut { get; private set; }

    /// <summary>Gets the transaction fees charged.</summary>
    public decimal Fees { get; private set; }

    /// <summary>Gets whether this transaction was added to or deducted from the client's credit balance.</summary>
    public bool AddedToCredit { get; private set; }

    /// <summary>Gets the ISO 4217 currency code the transaction's amounts are in (e.g. "USD"). Set at creation, never changed.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// Gets where the row came from. Set at creation, never changed: a gateway payment's payment method and
    /// reference are unique among the rows of this source, a manual entry's or an import's are not.
    /// </summary>
    public TransactionSource Source { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Transaction() { }

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
    /// <param name="currency">ISO 4217 currency code the amounts are in (e.g. "USD"); the related invoice's currency when there is one.</param>
    /// <param name="source">Where the row came from.</param>
    /// <returns>A new <see cref="Transaction"/>.</returns>
    /// <exception cref="DomainRuleException">Neither <paramref name="amountIn"/> nor <paramref name="amountOut"/> is positive.</exception>
    /// <exception cref="ArgumentException">The currency is not a three-letter code.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is not a defined value.</exception>
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
        bool addedToCredit,
        string currency,
        TransactionSource source)
    {
        EnsureMovesMoney(amountIn, amountOut);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "The transaction source is not a defined value.");
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
            Currency = CurrencyCode.Normalise(currency),
            Source = source,
        };
    }

    /// <summary>
    /// Creates the ledger row for a payment received against <paramref name="invoice"/>. The single
    /// place a payment's ledger entry is shaped, so every path that settles an invoice (gateway
    /// completion, manual payment, direct charge, card confirmation, bulk mark-paid) writes the
    /// same row: invoice currency, description and a zero debit.
    /// </summary>
    /// <param name="invoice">The invoice the payment settles.</param>
    /// <param name="date">UTC timestamp of the payment.</param>
    /// <param name="gateway">Payment method or gateway name recorded on the row.</param>
    /// <param name="transactionId">External transaction reference.</param>
    /// <param name="amount">Amount received (greater than 0).</param>
    /// <param name="fees">Transaction fees (≥ 0).</param>
    /// <param name="source"><see cref="TransactionSource.GatewayPayment"/> for money a gateway took, otherwise <see cref="TransactionSource.Manual"/>.</param>
    /// <returns>A new payment <see cref="Transaction"/> in the invoice's currency.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="invoice"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is neither a gateway payment nor a manual entry.</exception>
    /// <exception cref="DomainRuleException"><paramref name="amount"/> is not positive.</exception>
    public static Transaction CreateInvoicePayment(
        Invoice invoice,
        DateTimeOffset date,
        string gateway,
        string transactionId,
        decimal amount,
        decimal fees,
        TransactionSource source)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        EnsureSource(source, TransactionSource.GatewayPayment);

        return Create(
            invoice.ClientId,
            date,
            $"Invoice #{invoice.Id} Payment",
            transactionId,
            invoice.Id,
            gateway,
            amount,
            0m,
            fees,
            addedToCredit: false,
            invoice.Currency,
            source);
    }

    /// <summary>
    /// Creates the ledger row for a refund paid out against <paramref name="invoice"/>: invoice
    /// currency, description and a zero credit, the mirror of <see cref="CreateInvoicePayment"/>.
    /// </summary>
    /// <param name="invoice">The invoice refunded.</param>
    /// <param name="date">UTC timestamp of the refund.</param>
    /// <param name="gateway">Payment method or gateway name recorded on the row.</param>
    /// <param name="transactionId">Reference of the refund.</param>
    /// <param name="amount">Amount refunded (greater than 0).</param>
    /// <param name="addedToCredit">Whether the refund went to the client's credit balance.</param>
    /// <param name="source"><see cref="TransactionSource.GatewayRefund"/> for money a gateway returned, otherwise <see cref="TransactionSource.Manual"/>.</param>
    /// <returns>A new refund <see cref="Transaction"/> in the invoice's currency.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="invoice"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is neither a gateway refund nor a manual entry.</exception>
    /// <exception cref="DomainRuleException"><paramref name="amount"/> is not positive.</exception>
    public static Transaction CreateInvoiceRefund(
        Invoice invoice,
        DateTimeOffset date,
        string gateway,
        string transactionId,
        decimal amount,
        bool addedToCredit,
        TransactionSource source)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        EnsureSource(source, TransactionSource.GatewayRefund);

        return Create(
            invoice.ClientId,
            date,
            $"Invoice #{invoice.Id} Refund",
            transactionId,
            invoice.Id,
            gateway,
            0m,
            amount,
            0m,
            addedToCredit,
            invoice.Currency,
            source);
    }

    /// <summary>
    /// Corrects how the transaction is recorded: when it happened, what it was for, the external
    /// reference and the method used. The amounts and currency are not touched.
    /// </summary>
    /// <param name="date">UTC timestamp of the transaction.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="transactionId">External transaction reference.</param>
    /// <param name="paymentMethod">Payment method used.</param>
    public void CorrectDetails(DateTimeOffset date, string description, string transactionId, string paymentMethod)
    {
        Date = date;
        Description = description;
        TransactionId = transactionId;
        PaymentMethod = paymentMethod;
    }

    /// <summary>Points the transaction at the invoice it belongs to, or detaches it from any.</summary>
    /// <param name="invoiceId">The related invoice id, or <see langword="null"/> for none.</param>
    public void LinkToInvoice(int? invoiceId)
    {
        InvoiceId = invoiceId;
    }

    /// <summary>
    /// Corrects the money the transaction moved. The three amounts change together so the row
    /// never holds a half-corrected total.
    /// </summary>
    /// <param name="amountIn">Amount credited (≥ 0).</param>
    /// <param name="amountOut">Amount debited (≥ 0).</param>
    /// <param name="fees">Transaction fees (≥ 0).</param>
    /// <exception cref="DomainRuleException">Neither <paramref name="amountIn"/> nor <paramref name="amountOut"/> is positive.</exception>
    public void CorrectAmounts(decimal amountIn, decimal amountOut, decimal fees)
    {
        EnsureMovesMoney(amountIn, amountOut);

        AmountIn = amountIn;
        AmountOut = amountOut;
        Fees = fees;
    }

    /// <summary>
    /// Refuses a source an invoice payment or refund cannot have: it is the gateway one of its direction
    /// or a manual entry, never the other direction's gateway source and never an import.
    /// </summary>
    /// <param name="source">The source given.</param>
    /// <param name="gatewaySource">The gateway source that fits the row's direction.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is neither <paramref name="gatewaySource"/> nor Manual.</exception>
    private static void EnsureSource(TransactionSource source, TransactionSource gatewaySource)
    {
        if (source != gatewaySource && source != TransactionSource.Manual)
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "An invoice payment or refund is a gateway entry of its direction or a manual one.");
        }
    }

    /// <summary>The one invariant every transaction keeps: it moves money in at least one direction.</summary>
    /// <param name="amountIn">Amount credited.</param>
    /// <param name="amountOut">Amount debited.</param>
    /// <exception cref="DomainRuleException">Neither amount is positive.</exception>
    private static void EnsureMovesMoney(decimal amountIn, decimal amountOut)
    {
        if (amountIn <= 0 && amountOut <= 0)
        {
            throw new DomainRuleException(
                BillingErrors.TransactionAmountRequired,
                "At least one of AmountIn or AmountOut must be greater than 0.");
        }
    }
}
