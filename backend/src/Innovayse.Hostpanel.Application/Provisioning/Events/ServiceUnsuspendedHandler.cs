using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Application.Services.Commands.RestoreServiceSshKeys;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Events;

/// <summary>
/// Handles <see cref="ServiceUnsuspendedEvent"/> by reactivating the hosting account on the assigned
/// server's panel, then asking for the service's SSH keys to be put back. Delivered asynchronously.
/// </summary>
/// <remarks>
/// The keys are restored here a second time (the service's own unsuspension already asked) because
/// an agent refuses keys for a locked account, and the two unsuspensions are not ordered.
/// The event arrives through a durable queue, so it can be handled late: redelivered after a restart, or
/// retried behind a later suspension of the same service. The panel is therefore asked only while the
/// service is still active; a late unsuspension never unlocks an account the platform shows as suspended.
/// </remarks>
public sealed class ServiceUnsuspendedHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Sends the operation to the panel the account lives on and logs a failure.</summary>
    private readonly IHostedAccountOperations _hostedAccounts;

    /// <summary>Dispatches the SSH key restore once the account is unlocked.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a refused or failed key restore.</summary>
    private readonly ILogger<ServiceUnsuspendedHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="hostedAccounts">Sends the operation to the panel the account lives on and logs a failure.</param>
    /// <param name="bus">Dispatches the SSH key restore once the account is unlocked.</param>
    /// <param name="logger">Records a refused or failed key restore.</param>
    public ServiceUnsuspendedHandler(
        IHostpanelDbContext context, IHostedAccountOperations hostedAccounts, IMessageBus bus, ILogger<ServiceUnsuspendedHandler> logger)
    {
        _context = context;
        _hostedAccounts = hostedAccounts;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Reactivates the hosting account on the panel when a service is restored.
    /// </summary>
    /// <param name="evt">The domain event carrying the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the panel was asked and, when it unlocked the account, the keys were restored.</returns>
    public async Task HandleAsync(ServiceUnsuspendedEvent evt, CancellationToken ct)
    {
        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == evt.ServiceId, ct);
        // Gone (removed, or the demo reset truncated it) or no longer active: this delivery is stale.
        if (service is null || service.Status is not ServiceStatus.Active)
        {
            return;
        }

        // A failure is not acted on beyond the keys: the platform's record already changed, and the
        // operation has logged it for whoever has to bring the panel back in step.
        var outcome = await _hostedAccounts.UnsuspendAsync(
            new HostedAccountUnsuspension(HostedAccountTarget.Of(service)), ct);
        if (outcome is not HostedAccountOutcome.Updated)
        {
            return;
        }

        try
        {
            var restore = await _bus.InvokeAsync<Result>(new RestoreServiceSshKeysCommand(service.Id), ct);
            if (restore.IsFailure)
            {
                _logger.LogWarning(
                    "Restoring the SSH keys of service {ServiceId} was refused with {ErrorCode}.", service.Id, restore.ErrorCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The account is already unlocked; a key restore failure must not undo or hide that.
            _logger.LogError(ex, "Restoring the SSH keys of service {ServiceId} failed.", service.Id);
        }
    }
}
