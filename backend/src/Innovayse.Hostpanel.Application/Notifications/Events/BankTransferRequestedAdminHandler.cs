using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="BankTransferRequestedEvent"/> by mailing staff that a client chose to pay by bank transfer, so the
/// transfer is looked for in the bank and then confirmed or rejected in Pending bank transfers.
/// </summary>
public sealed class BankTransferRequestedAdminHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the invoice and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where staff mail goes.</summary>
    private readonly IOptions<NotificationOptions> _settings;

    /// <summary>Names the missing setting when no staff address is configured.</summary>
    private readonly ILogger<BankTransferRequestedAdminHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the invoice and sends the mail.</param>
    /// <param name="settings">Where staff mail goes.</param>
    /// <param name="logger">Names the missing setting when no staff address is configured.</param>
    public BankTransferRequestedAdminHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IOptions<NotificationOptions> settings,
        ILogger<BankTransferRequestedAdminHandler> logger)
    {
        _context = context;
        _bus = bus;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Mails staff that a transfer waits.</summary>
    /// <param name="evt">The transfer the client chose.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(BankTransferRequestedEvent evt, CancellationToken ct)
    {
        var to = StaffAlertAddress.Resolve(_settings.Value);
        if (to is null)
        {
            _logger.LogError(
                "Bank transfer notice for invoice {InvoiceId} not mailed: set {Setting} (or Notifications:ContactEmail).",
                evt.InvoiceId, "Notifications:AdminEmail");
            return;
        }

        // The invoice belongs to Billing: its facts come through Billing's own query, never its tables.
        var invoiceResult = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(evt.InvoiceId), ct);
        if (invoiceResult.IsFailure || invoiceResult.Value is not { } invoice)
        {
            return;
        }

        await BankTransferTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new BankTransferRequestedAdminTemplateModel(
            new InvoiceRefTemplateData(invoice.Id),
            new ClientRefTemplateData(evt.ClientId),
            evt.Reference,
            invoice.AmountDue,
            invoice.Currency);

        await _bus.InvokeAsync(
            new SendEmailCommand(
                to,
                null,
                BankTransferTemplatesSeeder.RequestedAdminSlug,
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, BankTransferTemplatesSeeder.RequestedAdminSlug)),
            ct);
    }
}
