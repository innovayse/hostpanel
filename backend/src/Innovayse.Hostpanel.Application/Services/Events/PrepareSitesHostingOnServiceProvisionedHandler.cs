using Innovayse.Hostpanel.Application.Services.Commands.PrepareSitesHosting;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Events;

/// <summary>
/// When a hosting account is provisioned, queues <see cref="PrepareSitesHostingCommand"/>: an AI Hosting product
/// (metadata <c>innovayseSites.prepare</c>) gets its account prepared for Innovayse Sites deploys. Published, not
/// invoked, so a preparation of several minutes never holds up the other handlers of the event (the welcome email).
/// </summary>
public sealed class PrepareSitesHostingOnServiceProvisionedHandler
{
    /// <summary>Queues the command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a failure to queue.</summary>
    private readonly ILogger<PrepareSitesHostingOnServiceProvisionedHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Queues the command.</param>
    /// <param name="logger">Records a failure to queue.</param>
    public PrepareSitesHostingOnServiceProvisionedHandler(IMessageBus bus, ILogger<PrepareSitesHostingOnServiceProvisionedHandler> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles <see cref="ServiceProvisionedEvent"/>.</summary>
    /// <param name="evt">The event.</param>
    /// <returns>A task that completes once the command is queued.</returns>
    public async Task HandleAsync(ServiceProvisionedEvent evt)
    {
        try
        {
            await _bus.PublishAsync(new PrepareSitesHostingCommand(evt.ServiceId));
        }
        catch (Exception ex)
        {
            // Wolverine runs every handler of one message in a single chain: never break the others.
            _logger.LogError(ex, "Queueing the Innovayse Sites preparation of service {ServiceId} failed.", evt.ServiceId);
        }
    }
}
