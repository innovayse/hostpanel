using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusComponentGroup;

/// <summary>Renames a status page group and persists it.</summary>
public sealed class UpdateStatusComponentGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateStatusComponentGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group and its new name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_COMPONENT_GROUP_NOT_FOUND</c> when the id names no group.</returns>
    public async Task<Result> HandleAsync(UpdateStatusComponentGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.StatusComponentGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_FOUND);
        }

        group.Rename(cmd.Name);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
