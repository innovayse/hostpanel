using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedAccountJail;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Events;

/// <summary>
/// Handles <see cref="ServiceTerminatedEvent"/> by removing the hosting account from the assigned
/// server's panel. Delivered asynchronously.
/// </summary>
public sealed class ServiceTerminatedHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Sends the operation to the panel the account lives on and logs a failure.</summary>
    private readonly IHostedAccountOperations _hostedAccounts;

    /// <summary>Queues the removal of the deleted account's SSH jail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="hostedAccounts">Sends the operation to the panel the account lives on and logs a failure.</param>
    /// <param name="bus">Queues the removal of the deleted account's SSH jail.</param>
    public ServiceTerminatedHandler(IHostpanelDbContext context, IHostedAccountOperations hostedAccounts, IMessageBus bus)
    {
        _bus = bus;
        _context = context;
        _hostedAccounts = hostedAccounts;
    }

    /// <summary>
    /// Removes the hosting account from the panel when a service is permanently terminated.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public async Task HandleAsync(ServiceTerminatedEvent evt, CancellationToken ct)
    {
        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == evt.ServiceId, ct);
        if (service is null)
        {
            return;
        }

        // The outcome is not acted on: the platform's record already changed, and a failure has
        // been logged by the operation for whoever has to bring the panel back in step.
        var outcome = await _hostedAccounts.TerminateAsync(
            new HostedAccountTermination(HostedAccountTarget.Of(service), evt.Reason), ct);
        if (outcome is HostedAccountOutcome.Updated)
        {
            // The panel deleted the account: its SSH jail on the server (kept for re-enrolment) can go too.
            await _bus.PublishAsync(new RemoveTerminatedAccountJailCommand(service.Id));
        }
    }
}
