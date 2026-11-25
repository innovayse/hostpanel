using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerBriefs;

/// <summary>Handles <see cref="GetServerBriefsQuery"/>: reads the named servers in one pass.</summary>
public sealed class GetServerBriefsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetServerBriefsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The servers to describe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One brief per server that exists, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ServerBriefDto>>> HandleAsync(GetServerBriefsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ServerIds.Count == 0)
        {
            return Result<IReadOnlyList<ServerBriefDto>>.Success([]);
        }

        // Read-only, so no tracking is needed.
        IReadOnlyList<ServerBriefDto> briefs = await _context.Servers
            .AsNoTracking()
            .Where(s => query.ServerIds.Contains(s.Id))
            .Select(s => new ServerBriefDto(s.Id, s.Name, s.Hostname, s.IpAddress, s.Module))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ServerBriefDto>>.Success(briefs);
    }
}
