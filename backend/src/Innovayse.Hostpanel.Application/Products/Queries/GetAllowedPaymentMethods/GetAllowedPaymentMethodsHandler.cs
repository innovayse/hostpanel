using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetAllowedPaymentMethods;

/// <summary>Handles <see cref="GetAllowedPaymentMethodsQuery"/> with one read of the products' groups.</summary>
public sealed class GetAllowedPaymentMethodsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetAllowedPaymentMethodsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Reads the groups and combines what they allow.</summary>
    /// <param name="query">The products.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> when no group is restricted (every method may be used); otherwise the methods all
    /// restricted groups share, which is empty when they share none. Never refuses.
    /// </returns>
    public async Task<Result<IReadOnlyList<string>?>> HandleAsync(GetAllowedPaymentMethodsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var groupIds = _context.Products
            .Where(p => query.ProductIds.Contains(p.Id))
            .Select(p => p.GroupId);

        var groups = await _context.ProductGroups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(ct);

        return Result<IReadOnlyList<string>?>.Success(ProductGroup.CommonAllowedPaymentMethods(groups));
    }
}
