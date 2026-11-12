using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusComponent;

/// <summary>Removes a status page component together with its check records and its place in maintenance windows. An incident the checks opened for it stays, for an operator to close.</summary>
public sealed class DeleteStatusComponentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteStatusComponentHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The component to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_COMPONENT_NOT_FOUND</c> when the id names no component.</returns>
    public async Task<Result> HandleAsync(DeleteStatusComponentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entity = await _context.StatusComponents.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (entity is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
        }

        _context.StatusComponents.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
