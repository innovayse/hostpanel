using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetProductSuspensions;

/// <summary>Handles <see cref="GetProductSuspensionsQuery"/>.</summary>
public sealed class GetProductSuspensionsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetProductSuspensionsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per suspended service, ordered by client name. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ProductSuspensionRowDto>>> HandleAsync(
        GetProductSuspensionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var raw = await _context.ClientServices.AsNoTracking()
            .Where(s => s.Status == ServiceStatus.Suspended)
            .Join(_context.Clients, s => s.ClientId, c => c.Id, (s, c) => new { s, c })
            .Join(_context.Products, x => x.s.ProductId, p => p.Id, (x, p) => new
            {
                ServiceId = x.s.Id,
                ClientName = x.c.FirstName + " " + x.c.LastName,
                ProductName = p.Name,
                x.s.Domain,
                x.s.NextRenewalAt,
                x.s.AdminNotes,
            })
            .OrderBy(r => r.ClientName)
            .ToListAsync(ct);

        IReadOnlyList<ProductSuspensionRowDto> rows = raw.Select(r => new ProductSuspensionRowDto(
            r.ServiceId,
            r.ClientName,
            r.ProductName,
            r.Domain,
            r.NextRenewalAt.HasValue ? r.NextRenewalAt.Value.ToString("yyyy-MM-dd") : null,
            r.AdminNotes)).ToList();

        return Result<IReadOnlyList<ProductSuspensionRowDto>>.Success(rows);
    }
}
