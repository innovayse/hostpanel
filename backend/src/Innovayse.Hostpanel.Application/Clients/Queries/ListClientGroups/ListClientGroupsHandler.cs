using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.ListClientGroups;

/// <summary>Returns every client group mapped to <see cref="ClientGroupDto"/>, with its client count.</summary>
public sealed class ListClientGroupsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListClientGroupsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every group, by name. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ClientGroupDto>>> HandleAsync(ListClientGroupsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var groups = await _context.ClientGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .SelectDto(_context.Clients.AsNoTracking())
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<ClientGroupDto>>(groups);
    }
}
