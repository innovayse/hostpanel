using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The transaction filters every transaction list shares: the client, the direction, the payment method, the period (calendar days, UTC) and the
/// amount. A list query that offers them implements this next to <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Common.TransactionListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Extensions.TransactionFilterExtensions"/>) are written once.
/// </summary>
public interface ITransactionListFilters
{
    /// <summary>Gets the id of the client whose transactions are kept; null keeps every client.</summary>
    public int? ClientId { get; }

    /// <summary>Gets the side of the ledger to keep; null keeps both.</summary>
    public TransactionDirection? Direction { get; }

    /// <summary>Gets the payment method to keep, compared as a whole; null or blank keeps every method.</summary>
    public string? PaymentMethod { get; }

    /// <summary>Gets the first day kept, inclusive; null for no lower bound.</summary>
    public DateOnly? From { get; }

    /// <summary>Gets the last day kept, the whole day included; null for no upper bound.</summary>
    public DateOnly? To { get; }

    /// <summary>Gets the amount to keep: the entries whose amount in or amount out is exactly this; null keeps every amount.</summary>
    public decimal? Amount { get; }
}
