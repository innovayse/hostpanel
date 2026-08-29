namespace Innovayse.Application.Reports.Queries.GetDomainRenewalEmails;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetDomainRenewalEmailsQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetDomainRenewalEmailsHandler(IReportRepository repo)
{
    /// <summary>Returns the renewal reminder emails that match the filters.</summary>
    /// <param name="query">Client, registrar, domain and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching reminder emails.</returns>
    public Task<DomainRenewalEmailsDto> HandleAsync(
        GetDomainRenewalEmailsQuery query, CancellationToken ct)
        => repo.GetDomainRenewalEmailsAsync(
            query.ClientId, query.Registrar, query.Domain, query.From, query.To, ct);
}
