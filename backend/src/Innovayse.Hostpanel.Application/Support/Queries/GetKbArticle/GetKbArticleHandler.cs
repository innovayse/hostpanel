using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.GetKbArticle;

/// <summary>Loads a single knowledge base article, published or not.</summary>
public sealed class GetKbArticleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetKbArticleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The article to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching article, or <c>KBARTICLE_NOT_FOUND</c> when the id names no row.</returns>
    public async Task<Result<KbArticleDto>> HandleAsync(GetKbArticleQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var row = await _context.KbArticles
            .AsNoTracking()
            .Where(a => a.Id == query.Id)
            .SelectRows(_context.KbCategories.AsNoTracking())
            .FirstOrDefaultAsync(ct);

        return row is null
            ? Result<KbArticleDto>.FailureCode(HostpanelErrorCodes.KBARTICLE_NOT_FOUND)
            : Result.Success(row.ToAdminDto());
    }
}
