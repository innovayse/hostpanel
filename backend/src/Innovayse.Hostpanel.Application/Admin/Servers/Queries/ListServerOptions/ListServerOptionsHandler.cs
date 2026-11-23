using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerOptions;

/// <summary>Loads every server as a <see cref="ServerOptionDto"/>, projected in SQL so no credential column is read.</summary>
public sealed class ListServerOptionsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListServerOptionsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The optional module filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every matching server, by name. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ServerOptionDto>>> HandleAsync(ListServerOptionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var options = await _context.Servers.AsNoTracking()
            .Matching(query.Module)
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .ToOptions()
            .ToListAsync(ct);

        return Result<IReadOnlyList<ServerOptionDto>>.Success(options);
    }
}
