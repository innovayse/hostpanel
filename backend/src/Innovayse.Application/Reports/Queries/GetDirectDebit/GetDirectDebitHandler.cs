namespace Innovayse.Application.Reports.Queries.GetDirectDebit;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetDirectDebitQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetDirectDebitHandler(IReportRepository repo)
{
    /// <summary>Returns the unpaid invoices assigned to the Direct Debit payment method.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoices awaiting collection by direct debit.</returns>
    public Task<DirectDebitDto> HandleAsync(GetDirectDebitQuery query, CancellationToken ct)
        => repo.GetDirectDebitAsync(ct);
}
