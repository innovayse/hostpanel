namespace Innovayse.Application.Billing.Events;

using Innovayse.Application.Services.Commands.SuspendService;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="InvoiceOverdueEvent"/> by automatically suspending active client services
/// that do not have the auto-suspend override flag set.
/// </summary>
public sealed class InvoiceOverdueAutoSuspendHandler(
    IClientServiceRepository serviceRepo,
    IMessageBus bus,
    ILogger<InvoiceOverdueAutoSuspendHandler> logger)
{
    /// <summary>
    /// Suspends all eligible active services for the client whose invoice became overdue.
    /// Services with <see cref="ClientService.OverrideAutoSuspend"/> set to <see langword="true"/> are skipped.
    /// </summary>
    /// <param name="evt">The overdue invoice event containing the client ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(InvoiceOverdueEvent evt, CancellationToken ct)
    {
        var services = await serviceRepo.ListByClientAsync(evt.ClientId, ct);

        var eligible = services
            .Where(s => s.Status == ServiceStatus.Active && !s.OverrideAutoSuspend)
            .ToList();

        foreach (var service in eligible)
        {
            await bus.InvokeAsync(new SuspendServiceCommand(service.Id), ct);
        }

        logger.LogInformation(
            "InvoiceOverdueAutoSuspend suspended {Count} services for client {ClientId} (invoice {InvoiceId}).",
            eligible.Count, evt.ClientId, evt.InvoiceId);
    }
}
