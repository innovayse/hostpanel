using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.DeleteCancellationRequest;

/// <summary>Deletes a cancellation request by primary key.</summary>
public sealed class DeleteCancellationRequestHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteCancellationRequestHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The cancellation request to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CANCELLATION_REQUEST_NOT_FOUND</c> when the id names no request.</returns>
    public async Task<Result> HandleAsync(DeleteCancellationRequestCommand cmd, CancellationToken ct)
    {
        // Tracked, not AsNoTracking: the entity
        // is removed below and saved in this same pass.
        var request = await _context.CancellationRequests.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (request is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CANCELLATION_REQUEST_NOT_FOUND);
        }

        _context.CancellationRequests.Remove(request);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
