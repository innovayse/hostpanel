using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Provisioning.Events;

/// <summary>
/// Handles <see cref="ServiceSuspendedEvent"/> by suspending the hosting account on the assigned
/// server's panel. Delivered asynchronously.
/// </summary>
/// <remarks>
/// The event arrives through a durable queue, so it can be handled late: redelivered after a restart, or
/// retried behind a later unsuspension of the same service. The panel is therefore asked only while the
/// service is still suspended; a late suspension never locks an account the platform shows as active.
/// </remarks>
public sealed class ServiceSuspendedHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Sends the operation to the panel the account lives on and logs a failure.</summary>
    private readonly IHostedAccountOperations _hostedAccounts;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="hostedAccounts">Sends the operation to the panel the account lives on and logs a failure.</param>
    public ServiceSuspendedHandler(IHostpanelDbContext context, IHostedAccountOperations hostedAccounts)
    {
        _context = context;
        _hostedAccounts = hostedAccounts;
    }

    /// <summary>
    /// Suspends the hosting account on the panel when a service is suspended.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the panel was asked, or at once for a stale delivery.</returns>
    public async Task HandleAsync(ServiceSuspendedEvent evt, CancellationToken ct)
    {
        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == evt.ServiceId, ct);
        // Gone (removed, or the demo reset truncated it) or no longer suspended: this delivery is stale.
        if (service is null || service.Status is not ServiceStatus.Suspended)
        {
            return;
        }

        // The outcome is not acted on: the platform's record already changed, and a failure has
        // been logged by the operation for whoever has to bring the panel back in step.
        await _hostedAccounts.SuspendAsync(
            new HostedAccountSuspension(HostedAccountTarget.Of(service), evt.Reason), ct);
    }
}
