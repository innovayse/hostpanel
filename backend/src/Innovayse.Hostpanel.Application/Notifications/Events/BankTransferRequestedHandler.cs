using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
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
/// Handles <see cref="BankTransferRequestedEvent"/> by mailing the client the bank details and the reference to write on
/// the transfer, so they have them outside the page too.
/// </summary>
public sealed class BankTransferRequestedHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the invoice, the client and the bank details, and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client area lives, for the invoice link.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the invoice, the client and the bank details, and sends the mail.</param>
    /// <param name="clientPortal">Where the client area lives, for the invoice link.</param>
    public BankTransferRequestedHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<ClientPortalOptions> clientPortal)
    {
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Mails the client how to pay the invoice by bank transfer.</summary>
    /// <param name="evt">The transfer the client chose.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(BankTransferRequestedEvent evt, CancellationToken ct)
    {
        // The invoice belongs to Billing: its facts come through Billing's own query, never its tables.
        var invoiceResult = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(evt.InvoiceId), ct);
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (invoiceResult.IsFailure || invoiceResult.Value is not { } invoice || client is not { Email: { } email })
        {
            return;
        }

        await BankTransferTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var settings = await _bus.LoadBankTransferSettingsAsync(ct);

        var data = new BankTransferDetailsTemplateModel(
            new BankTransferInvoiceTemplateData(
                invoice.Id,
                invoice.AmountDue,
                invoice.Currency,
                evt.Reference,
                $"{_clientPortal.Value.BaseUrl.TrimEnd('/')}/client/invoices/{invoice.Id}"),
            new BankTransferBankTemplateData(
                settings.AccountName ?? string.Empty,
                settings.Iban ?? string.Empty,
                settings.Swift ?? string.Empty,
                settings.BankName ?? string.Empty,
                settings.Instructions ?? string.Empty));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                BankTransferTemplatesSeeder.DetailsSlug,
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Invoice),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, BankTransferTemplatesSeeder.DetailsSlug)),
            ct);
    }
}
