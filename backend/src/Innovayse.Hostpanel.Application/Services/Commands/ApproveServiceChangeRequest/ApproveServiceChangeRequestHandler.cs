using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.ApproveServiceChangeRequest;

/// <summary>
/// Approves a pending plan change request and applies it through <see cref="CompleteServiceChangeCommand"/>, the same
/// use case an automatic approval ends in. Approving a request that is already Approved applies it again: that is how
/// staff retry a change the hosting panel refused.
/// </summary>
/// <remarks>
/// The approval is saved before the change is applied, so a panel that refuses leaves the request Approved, visible to
/// staff and safe to retry, rather than Pending as if nobody had acted.
/// </remarks>
public sealed class ApproveServiceChangeRequestHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to apply the change through its own use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to apply the change through its own use case.</param>
    public ApproveServiceChangeRequestHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The request to approve and an optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>SERVICE_CHANGE_REQUEST_NOT_FOUND</c> when the id names no request, <c>SERVICE_CHANGE_INVALID_STATE</c>
    /// when the request is already Completed or Rejected, or the failure of applying the change.
    /// </returns>
    public async Task<Result> HandleAsync(ApproveServiceChangeRequestCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var request = await _context.ServiceChangeRequests.FirstOrDefaultAsync(r => r.Id == cmd.RequestId, ct);
        if (request is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_REQUEST_NOT_FOUND);
        }

        if (request.Status is ServiceChangeStatus.Pending)
        {
            request.Approve(cmd.Note);
            await _context.SaveChangesAsync(ct);
        }
        else if (request.Status is not ServiceChangeStatus.Approved)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
        }

        return await _bus.InvokeAsync<Result>(new CompleteServiceChangeCommand(request.Id), ct);
    }
}
