using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListTransactions;

/// <summary>Query to list the transactions of every client, or of one, narrowed by a search and filters; the summary totals follow the same rows.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only transactions whose description, reference or payment method contain it, or (on the list of every
/// client) that belong to a client whose first name, last name, company, sign-in address or account name contains it
/// (case-insensitive, a <c>%</c> or <c>_</c> matches itself), or whose id it is when it is a whole number, are listed.
/// Blank lists all.
/// </param>
/// <param name="ClientId">The client's primary key, or <see langword="null"/> for all clients; the controller overlays it from the route on a client's list.</param>
/// <param name="Direction">Optional direction, bound by name from the query string; <see langword="null"/> for money in and out.</param>
/// <param name="PaymentMethod">Optional payment method, compared as a whole; <see langword="null"/> or blank for every method.</param>
/// <param name="From">Optional first day kept (UTC), bound from <c>yyyy-MM-dd</c>; <see langword="null"/> for no lower bound.</param>
/// <param name="To">Optional last day kept (UTC), bound from <c>yyyy-MM-dd</c>, the whole day included; <see langword="null"/> for no upper bound.</param>
/// <param name="Amount">Optional amount; only entries whose amount in or amount out is exactly this are listed. <see langword="null"/> for every amount.</param>
public sealed record ListTransactionsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? ClientId = null,
    TransactionDirection? Direction = null,
    string? PaymentMethod = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? Amount = null) : IListQuery, ITransactionListFilters;
