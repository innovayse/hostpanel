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
/// Handles <see cref="CreditNoteIssuedEvent"/> by mailing the client the credit note that was issued to them, in their language,
/// with a link to the credit notes page of the client area where the PDF can be downloaded.
/// </summary>
public sealed class CreditNoteIssuedHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the client and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client area lives, for the credit notes page link.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the client and sends the mail.</param>
    /// <param name="clientPortal">Where the client area lives, for the credit notes page link.</param>
    public CreditNoteIssuedHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<ClientPortalOptions> clientPortal)
    {
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Mails the client that a credit note was issued.</summary>
    /// <param name="evt">The issued credit note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(CreditNoteIssuedEvent evt, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return;
        }

        await CreditNoteTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new CreditNoteIssuedTemplateModel(new CreditNoteTemplateData(
            evt.Number,
            evt.Total,
            evt.Currency,
            evt.InvoiceId,
            $"{_clientPortal.Value.BaseUrl.TrimEnd('/')}/client/credit-notes"));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                CreditNoteTemplatesSeeder.IssuedSlug,
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Invoice),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, CreditNoteTemplatesSeeder.IssuedSlug)),
            ct);
    }
}
