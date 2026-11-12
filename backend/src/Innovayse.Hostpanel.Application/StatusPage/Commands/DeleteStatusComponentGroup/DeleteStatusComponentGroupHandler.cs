using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusComponentGroup;

/// <summary>Removes a status page group that lists no components.</summary>
public sealed class DeleteStatusComponentGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteStatusComponentGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>STATUS_COMPONENT_GROUP_NOT_FOUND</c> when the id names no group. A group that still has components is
    /// refused by the aggregate as <c>STATUS_COMPONENT_GROUP_NOT_EMPTY</c>.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteStatusComponentGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.StatusComponentGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_FOUND);
        }

        group.EnsureRemovable();
        _context.StatusComponentGroups.Remove(group);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
