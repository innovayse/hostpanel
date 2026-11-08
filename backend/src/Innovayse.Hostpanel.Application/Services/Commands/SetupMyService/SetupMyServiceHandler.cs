using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SetupService;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.SetupMyService;

/// <summary>
/// Sets up and provisions one of the calling client's own pending services, refusing every service
/// that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// provision through <see cref="SetupMyServiceCommand"/> without it having run. Refusing first
/// also keeps a stranger's id away from <see cref="SetupServiceCommand"/>, whose own refusals
/// distinguish "not found" from "not pending" and would otherwise report the state of somebody
/// else's service. Once ownership is settled the work is unchanged, so this dispatches the shared
/// command rather than duplicating provisioning.
/// <para>
/// A client provisions only what is paid for: unless the first payment is free, the service's
/// first invoice must be paid in full. Staff provisioning goes through its own route and is not
/// bound by this.
/// </para>
/// </remarks>
public sealed class SetupMyServiceHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>The module's data context, the service's billing facts are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to read the first invoice and to reach the shared use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="context">The module's data context, the service's billing facts are read from.</param>
    /// <param name="bus">Wolverine bus, used to read the first invoice and to reach the shared use case.</param>
    public SetupMyServiceHandler(IServiceOwnership ownership, IHostpanelDbContext context, IMessageBus bus)
    {
        _ownership = ownership;
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, when no such
    /// service exists, or when the caller has no client record, <c>SERVICE_FIRST_INVOICE_UNPAID</c>
    /// when the service names a first invoice for a non-free first payment and that invoice is not paid in full, or <c>SERVICE_NOT_PENDING</c>,
    /// <c>NO_ELIGIBLE_SERVER</c>, <c>PROVISIONING_UPSTREAM_FAILED</c> or <c>PROVISIONING_OUTCOME_UNKNOWN</c> from the shared use case.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(SetupMyServiceCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        var paidOutcome = await RequireFirstInvoicePaidAsync(cmd.ServiceId, ct);
        if (paidOutcome.IsFailure)
        {
            return paidOutcome;
        }

        return await _bus.InvokeAsync<Result>(
            new SetupServiceCommand(cmd.ServiceId, cmd.Domain, cmd.Username, cmd.Password), ct);
    }

    /// <summary>
    /// Refuses a service whose first payment is not free unless the invoice it is charged on is
    /// paid in full. A service that names no first invoice is let through, see
    /// <see cref="Domain.Services.ClientService.RequiresPaidFirstInvoice"/>.
    /// </summary>
    /// <param name="serviceId">The caller's own service, ownership already settled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SERVICE_FIRST_INVOICE_UNPAID</c>.</returns>
    private async Task<Result> RequireFirstInvoicePaidAsync(int serviceId, CancellationToken ct)
    {
        var service = await _context.ClientServices
            .AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, ct);
        if (!service.RequiresPaidFirstInvoice || service.FirstInvoiceId is not { } invoiceId)
        {
            return Result.Success();
        }

        // The invoice belongs to Billing, so its status is asked through Billing's own query.
        var invoice = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct);
        var paidInFull = invoice.IsSuccess && invoice.Value!.IsPaidInFull;
        return paidInFull
            ? Result.Success()
            : Result.FailureCode(HostpanelErrorCodes.SERVICE_FIRST_INVOICE_UNPAID);
    }
}
