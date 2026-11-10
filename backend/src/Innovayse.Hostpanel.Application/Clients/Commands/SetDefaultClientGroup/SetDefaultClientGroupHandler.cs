using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.SetDefaultClientGroup;

/// <summary>
/// Makes a client group the one default, clearing the flag on the group that held it, or clears the
/// flag so the group is no longer the default.
/// </summary>
public sealed class SetDefaultClientGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public SetDefaultClientGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group and whether it is to be the default.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CLIENT_GROUP_NOT_FOUND</c> when the id names no group.</returns>
    public async Task<Result> HandleAsync(SetDefaultClientGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // The current default first, and that order is load-bearing: one filtered unique index allows a
        // single default row, and EF writes its UPDATEs in the order the entities were tracked. Loaded
        // in any other order, the new default can be written before the old one is cleared, and
        // Postgres refuses the whole save.
        var candidates = await _context.ClientGroups
            .Where(g => g.IsDefault || g.Id == cmd.Id)
            .OrderByDescending(g => g.IsDefault)
            .ToListAsync(ct);

        var group = candidates.Find(g => g.Id == cmd.Id);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        }

        if (cmd.IsDefault)
        {
            group.MakeDefault(candidates);
        }
        else
        {
            group.ClearDefault();
        }

        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
