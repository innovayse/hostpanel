using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.DeleteClientGroup;

/// <summary>Deletes a client group, refusing while any client still belongs to it.</summary>
public sealed class DeleteClientGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteClientGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>CLIENT_GROUP_NOT_FOUND</c> when the id names no group, or <c>CLIENT_GROUP_IN_USE</c>
    /// when a client still belongs to it.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteClientGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.ClientGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        }

        if (await _context.Clients.AnyAsync(c => c.ClientGroupId == cmd.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_GROUP_IN_USE);
        }

        _context.ClientGroups.Remove(group);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
