using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles;

/// <summary>Lists every knowledge base article, published or not, with all its translations.</summary>
public sealed class ListKbArticlesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListKbArticlesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every article, oldest first.</returns>
    public async Task<Result<IReadOnlyList<KbArticleDto>>> HandleAsync(ListKbArticlesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _context.KbArticles
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .SelectRows(_context.KbCategories.AsNoTracking())
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<KbArticleDto>>(rows.Select(r => r.ToAdminDto()).ToList());
    }
}
