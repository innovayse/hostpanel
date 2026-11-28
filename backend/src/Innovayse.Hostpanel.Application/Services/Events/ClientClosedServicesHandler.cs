using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Events;

/// <summary>
/// Suspends every active service of a closed account through <see cref="SuspendServiceCommand"/>, the
/// one use case every suspension goes through, so the hosting account is suspended on its panel too.
/// The suspension is <see cref="SuspensionSource.Other"/>: a payment never lifts it, and that stored
/// source is the reason; no sentence is written, so none can be left in one language. A
/// non-notification handler, and safe as one: suspending is refused for a service that is no longer
/// active, and a failure is logged for staff.
/// </summary>
public sealed class ClientClosedServicesHandler
{
    /// <summary>The module's data context, to find the client's active services.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Dispatches the suspensions.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a service that could not be suspended, so staff can settle it by hand.</summary>
    private readonly ILogger<ClientClosedServicesHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to find the client's active services.</param>
    /// <param name="bus">Dispatches the suspensions.</param>
    /// <param name="logger">Records a service that could not be suspended.</param>
    public ClientClosedServicesHandler(IHostpanelDbContext context, IMessageBus bus, ILogger<ClientClosedServicesHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Suspends the closed client's active services.</summary>
    /// <param name="evt">The closing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task HandleAsync(ClientClosedEvent evt, CancellationToken ct)
    {
        try
        {
            var serviceIds = await _context.ClientServices.AsNoTracking()
                .Where(s => s.ClientId == evt.ClientId && s.Status == ServiceStatus.Active)
                .Select(s => s.Id)
                .ToListAsync(ct);

            foreach (var serviceId in serviceIds)
            {
                var outcome = await _bus.InvokeAsync<Result>(
                    new SuspendServiceCommand(serviceId, SuspensionSource.Other), ct);
                if (outcome.IsFailure)
                {
                    _logger.LogWarning(
                        "Suspending service {ServiceId} of closed client {ClientId} was refused with {ErrorCode}.",
                        serviceId, evt.ClientId, outcome.ErrorCode);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Wolverine combines every handler of one message into a single chain, so a throw here
            // would also stop the other handlers of the closing.
            _logger.LogError(ex, "Suspending the services of closed client {ClientId} failed.", evt.ClientId);
        }
    }
}
