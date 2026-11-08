using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.SetupService;

/// <summary>
/// Handles <see cref="SetupServiceCommand"/>: stores the hosting account details on the pending
/// service, then provisions it through <see cref="ProvisionServiceCommand"/>, the one place a
/// hosting account is created.
/// </summary>
public sealed class SetupServiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to provision once the details are stored.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to provision once the details are stored.</param>
    public SetupServiceHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Stores the domain, username and password on the service and provisions it.
    /// </summary>
    /// <param name="cmd">The setup command with hosting details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_PENDING</c>
    /// when the service is not Pending, or any refusal of <see cref="ProvisionServiceCommand"/>
    /// (<c>PRODUCT_PACKAGE_MISSING</c>, <c>NO_ELIGIBLE_SERVER</c>, <c>PROVISIONING_UPSTREAM_FAILED</c>, <c>PROVISIONING_OUTCOME_UNKNOWN</c>).
    /// </returns>
    public async Task<Result> HandleAsync(SetupServiceCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.Status is not ServiceStatus.Pending)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_PENDING);
        }

        // Saved before provisioning: the provisioning use case reads the details from the stored
        // service, and a refused attempt keeps them so the next attempt does not need them again.
        service.AssignHostingAccount(cmd.Domain.Trim(), cmd.Username, cmd.Password);
        await _context.SaveChangesAsync(ct);

        return await _bus.InvokeAsync<Result>(new ProvisionServiceCommand(service.Id), ct);
    }
}
