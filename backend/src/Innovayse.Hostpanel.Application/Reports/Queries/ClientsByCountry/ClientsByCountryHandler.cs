using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.ClientsByCountry;

/// <summary>Handles <see cref="ClientsByCountryQuery"/>.</summary>
public sealed class ClientsByCountryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ClientsByCountryHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Client counts and paid-invoice revenue grouped by country, largest count first. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ClientsByCountryDto>>> HandleAsync(
        ClientsByCountryQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var counts = await _context.Clients
            .AsNoTracking()
            .GroupBy(c => c.Country)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var revenue = await _context.Invoices
            .AsNoTracking()
            .Where(i => Invoice.PaidStatuses.Contains(i.Status))
            .Join(_context.Clients.AsNoTracking(), i => i.ClientId, c => c.Id, (i, c) => new { c.Country, i.Total })
            .GroupBy(x => x.Country)
            .Select(g => new { Country = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        IReadOnlyList<ClientsByCountryDto> rows = counts
            .OrderByDescending(x => x.Count)
            .Select(x => new ClientsByCountryDto(
                Country: string.IsNullOrWhiteSpace(x.Country) ? null : x.Country,
                ClientCount: x.Count,
                TotalRevenue: revenue.FirstOrDefault(r => r.Country == x.Country)?.Total ?? 0m))
            .ToList();

        return Result<IReadOnlyList<ClientsByCountryDto>>.Success(rows);
    }
}
