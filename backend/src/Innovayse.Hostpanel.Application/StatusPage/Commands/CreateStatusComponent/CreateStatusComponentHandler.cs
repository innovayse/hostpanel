using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponent;

/// <summary>Adds a component to a status page group and persists it.</summary>
public sealed class CreateStatusComponentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateStatusComponentHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The component to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new component's id, or <c>STATUS_COMPONENT_GROUP_NOT_FOUND</c> when the group id names no group.</returns>
    public async Task<Result<int>> HandleAsync(CreateStatusComponentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.StatusComponentGroups.FirstOrDefaultAsync(g => g.Id == cmd.GroupId, ct);
        if (group is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_FOUND);
        }

        var component = group.AddComponent(cmd.Name, cmd.Description, cmd.DisplayOrder);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(component.Id);
    }
}
