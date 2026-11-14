using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.FindProductIdsByName;

/// <summary>Handles <see cref="FindProductIdsByNameQuery"/>: matches the text against the product names.</summary>
public sealed class FindProductIdsByNameHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public FindProductIdsByNameHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The text to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the products with a matching name, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(FindProductIdsByNameQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return Result<IReadOnlyList<int>>.Success([]);
        }

        IReadOnlyList<int> productIds = await _context.Products
            .AsNoTracking()
            .Search(query.Search, p => p.Name)
            .Select(p => p.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(productIds);
    }
}
