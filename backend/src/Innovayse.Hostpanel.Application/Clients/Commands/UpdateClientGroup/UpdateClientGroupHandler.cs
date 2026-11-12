using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateClientGroup;

/// <summary>Updates a client group's name, description, discount and colour.</summary>
public sealed class UpdateClientGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateClientGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group to update and its new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>CLIENT_GROUP_NOT_FOUND</c> when the id names no group, or <c>CLIENT_GROUP_NAME_ALREADY_EXISTS</c>
    /// when another group has the name.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateClientGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.ClientGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        }

        group.Update(cmd.Name, cmd.Description, cmd.DiscountPercent, cmd.Color);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The unique index on the name refused the new name: another group already has it.
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_GROUP_NAME_ALREADY_EXISTS);
        }

        return Result.Success();
    }
}
