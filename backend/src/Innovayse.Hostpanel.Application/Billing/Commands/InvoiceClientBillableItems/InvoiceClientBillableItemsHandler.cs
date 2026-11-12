using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.InvoiceClientBillableItems;

/// <summary>
/// Handles <see cref="InvoiceClientBillableItemsCommand"/>: one invoice for the client's due items of
/// one type, written so that a crash can never leave an invoice with its items still due (which the
/// next day's run would invoice a second time).
/// </summary>
/// <remarks>
/// A recurring item is re-armed in the same single save that adds the invoice. A one-time item must
/// record the id the database gives the invoice, so the invoice and the marking are two saves inside
/// one transaction: both commit or neither does.
/// </remarks>
public sealed class InvoiceClientBillableItemsHandler
{
    /// <summary>Default payment term in days for auto-generated invoices.</summary>
    private const int PaymentTermDays = 14;

    /// <summary>The module's data context, for the billable items due and the invoice created for them.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The currency the invoiced client is billed in, stamped on the invoice.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Fixes the tax rate the invoice is raised at.</summary>
    private readonly ClientTaxRateResolver _taxRates;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<InvoiceClientBillableItemsHandler> _logger;

    /// <summary>The clock the invoice moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the billable items due and the invoice created for them.</param>
    /// <param name="payerCurrency">The currency the invoiced client is billed in, stamped on the invoice.</param>
    /// <param name="taxRates">Fixes the tax rate the invoice is raised at.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the invoice moment is read from.</param>
    public InvoiceClientBillableItemsHandler(
        IHostpanelDbContext context,
        IPayerCurrencyResolver payerCurrency,
        ClientTaxRateResolver taxRates,
        ILogger<InvoiceClientBillableItemsHandler> logger,
        IClock clock)
    {
        _context = context;
        _payerCurrency = payerCurrency;
        _taxRates = taxRates;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Invoices the client's due items of the requested type.</summary>
    /// <param name="cmd">The client and the item type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, including when nothing was due any more, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public async Task<Result> HandleAsync(InvoiceClientBillableItemsCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // Tracked, not AsNoTracking: each item is marked below and saved in this same unit of work.
        var items = await _context.BillableItems
            .DueForInvoicing(cmd.Type, now)
            .Where(x => x.ClientId == cmd.ClientId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            return Result.Success();
        }

        var currency = (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;
        var invoice = Invoice.CreateForBillableItems(cmd.ClientId, now.AddDays(PaymentTermDays), currency, items, now);
        var taxed = await _taxRates.ApplyToAsync(invoice, ct);
        if (taxed.IsFailure)
        {
            return taxed;
        }

        _context.Invoices.Add(invoice);

        if (cmd.Type == BillableItemType.Recurring)
        {
            // AdvanceDueDate re-arms the item (no invoiced flag, no invoice id), so there is nothing
            // to record once the invoice has its id: invoice and re-arming commit in one save.
            foreach (var item in items)
            {
                item.AdvanceDueDate();
            }

            await _context.SaveChangesAsync(ct);
        }
        else
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            await _context.SaveChangesAsync(ct);
            foreach (var item in items)
            {
                item.MarkInvoiced(invoice.Id);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        _logger.LogInformation(
            "Created invoice {InvoiceId} for client {ClientId} with {Count} {Type} billable items.",
            invoice.Id, cmd.ClientId, items.Count, cmd.Type);
        return Result.Success();
    }
}
