namespace Innovayse.Application.Domains.Queries.ListDomains;

using Innovayse.Application.Common;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>Returns a paginated list of all domains for the admin view.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="clientRepo">Client repository for resolving client names.</param>
public sealed class ListDomainsHandler(IDomainRepository repo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="ListDomainsQuery"/>.
    /// </summary>
    /// <param name="query">The list domains query with pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing domain registration items with client names.</returns>
    public async Task<PagedResult<DomainRegistrationDto>> HandleAsync(ListDomainsQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await repo.PagedListAsync(query.Page, query.PageSize, ct, query.ClientId);

        // Batch-resolve client names
        var clientIds = items.Select(d => d.ClientId).Distinct().ToList();
        var clients = new Dictionary<int, string>();
        foreach (var cid in clientIds)
        {
            var client = await clientRepo.FindByIdAsync(cid, ct);
            if (client is not null)
                clients[cid] = $"{client.FirstName} {client.LastName}";
        }

        var dtos = items
            .Select(d => new DomainRegistrationDto(
                d.Id,
                d.ClientId,
                clients.GetValueOrDefault(d.ClientId, "Unknown"),
                d.Name,
                FormatRegPeriod(d.RegistrationPeriod),
                d.Registrar,
                d.RecurringAmount,
                d.PriceCurrency,
                d.NextDueDate,
                d.ExpiresAt,
                d.Status,
                d.AutoRenew))
            .ToList();

        return new PagedResult<DomainRegistrationDto>(dtos, totalCount, query.Page, query.PageSize);
    }

    /// <summary>Formats registration period years as a label.</summary>
    /// <param name="years">Number of years.</param>
    /// <returns>Label like "1 Year" or "2 Years".</returns>
    private static string FormatRegPeriod(int years) =>
        years <= 0 ? "N/A" : years == 1 ? "1 Year" : $"{years} Years";
}
