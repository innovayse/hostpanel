using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.SetStatusComponentState;

/// <summary>Sets the state the status page shows for a component.</summary>
public sealed class SetStatusComponentStateHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public SetStatusComponentStateHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The component and its new state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_COMPONENT_NOT_FOUND</c> when the id names no component.</returns>
    public async Task<Result> HandleAsync(SetStatusComponentStateCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var component = await _context.StatusComponents.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (component is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
        }

        component.SetState(cmd.State);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
