using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Services.Events;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>Handles <see cref="TrialEndedEvent"/> by mailing the client that their free trial ended and the service was closed, in their language, with a link to the service.</summary>
public sealed class TrialEndedHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the service and the client and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client area lives, for the service link.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the service and the client and sends the mail.</param>
    /// <param name="clientPortal">Where the client area lives, for the service link.</param>
    public TrialEndedHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<ClientPortalOptions> clientPortal)
    {
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Mails the client.</summary>
    /// <param name="evt">The trial event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(TrialEndedEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        await _bus.SendTrialEmailAsync(
            _context,
            _clientPortal.Value,
            evt,
            evt.ServiceId,
            evt.ClientId,
            evt.TrialEndsAt,
            TrialTemplatesSeeder.EndedSlug,
            trial => new TrialEndedTemplateModel(trial),
            ct);
    }
}
