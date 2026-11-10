using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetClientGroup;

/// <summary>Loads a single client group and maps it to <see cref="ClientGroupDto"/>.</summary>
public sealed class GetClientGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetClientGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The group id to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The group, or <c>CLIENT_GROUP_NOT_FOUND</c> when the id names no group.</returns>
    public async Task<Result<ClientGroupDto>> HandleAsync(GetClientGroupQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var group = await _context.ClientGroups
            .AsNoTracking()
            .Where(g => g.Id == query.Id)
            .SelectDto(_context.Clients.AsNoTracking())
            .FirstOrDefaultAsync(ct);
        if (group is null)
        {
            return Result<ClientGroupDto>.FailureCode(HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        }

        return Result<ClientGroupDto>.Success(group);
    }
}
