using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="CreditBalanceChangedEvent"/> by mailing the client the change and the new balance. The event is raised
/// only for the movements the client did not start and cannot see coming (a top-up, a refund to the balance, a payment beyond
/// an invoice, an adjustment by staff).
/// </summary>
public sealed class CreditBalanceChangedHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the client and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client area lives, for the credit page link.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the client and sends the mail.</param>
    /// <param name="clientPortal">Where the client area lives, for the credit page link.</param>
    public CreditBalanceChangedHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<ClientPortalOptions> clientPortal)
    {
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Mails the client that the balance changed.</summary>
    /// <param name="evt">The balance change.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(CreditBalanceChangedEvent evt, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return;
        }

        await CreditBalanceTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new CreditBalanceChangedTemplateModel(new CreditBalanceTemplateData(
            evt.Change,
            evt.BalanceAfter,
            evt.Currency,
            $"{_clientPortal.Value.BaseUrl.TrimEnd('/')}/client/credit"));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                CreditBalanceTemplatesSeeder.ChangedSlug,
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Invoice),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, CreditBalanceTemplatesSeeder.ChangedSlug)),
            ct);
    }
}
