using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="StoredChargeActionRequiredEvent"/> by mailing the client the link at which the bank asks them
/// to confirm the card charge.
/// </summary>
public sealed class StoredChargeActionRequiredHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the client and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client area lives, for the invoice link.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the client and sends the mail.</param>
    /// <param name="clientPortal">Where the client area lives, for the invoice link.</param>
    public StoredChargeActionRequiredHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<ClientPortalOptions> clientPortal)
    {
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Mails the client the confirmation link.</summary>
    /// <param name="evt">The charge that waits for the payer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(StoredChargeActionRequiredEvent evt, CancellationToken ct)
    {
        // The invoice belongs to Billing: its facts come through Billing's own query, never its tables.
        var invoiceResult = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(evt.InvoiceId), ct);
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (invoiceResult.IsFailure || invoiceResult.Value is not { } invoice || client is not { Email: { } email })
        {
            return;
        }

        await CardChargeTemplatesSeeder.EnsureSeededAsync(_context, ct);

        var invoiceUrl = $"{_clientPortal.Value.BaseUrl.TrimEnd('/')}/client/invoices/{invoice.Id}";
        var data = new CardChargeActionRequiredTemplateModel(
            new ChargedInvoiceTemplateData(invoice.Id, invoice.Total, invoice.Currency, invoiceUrl),
            // A plugin that gave no address still leaves the client a place to finish: the invoice page.
            string.IsNullOrWhiteSpace(evt.ActionUrl) ? invoiceUrl : evt.ActionUrl);

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                CardChargeTemplatesSeeder.ActionRequiredSlug,
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Invoice),
                IdempotencyKey: EmailIdempotencyKey.ForParts(
                    CardChargeTemplatesSeeder.ActionRequiredSlug,
                    evt.ClaimId.ToString(CultureInfo.InvariantCulture))),
            ct);
    }
}
