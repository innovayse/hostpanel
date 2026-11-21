using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductGroups;

/// <summary>Returns product groups as DTOs, the product count of each included.</summary>
public sealed class GetProductGroupsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public GetProductGroupsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="qry">Whether to list only the active groups.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching groups, by name. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ProductGroupDto>>> HandleAsync(
        GetProductGroupsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var groups = await _context.ProductGroups
            .AsNoTracking()
            .Where(g => !qry.ActiveOnly || g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new ProductGroupDto(
                g.Id, g.Name, g.Description, g.IsActive, g.Products.Count, g.AllowedPaymentMethods))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProductGroupDto>>.Success(groups);
    }
}
