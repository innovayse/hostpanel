using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.InvoiceSelectedItems;

/// <summary>
/// Creates an invoice from selected uninvoiced billable items and marks them as invoiced.
/// </summary>
public sealed class InvoiceSelectedItemsHandler
{
    /// <summary>How long after creation an invoice for selected items falls due.</summary>
    private static readonly TimeSpan PaymentTerm = TimeSpan.FromDays(14);

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the currency the invoiced client is billed in, stamped on the invoice.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Fixes the tax rate the invoice is raised at.</summary>
    private readonly ClientTaxRateResolver _taxRates;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">The currency the invoiced client is billed in, stamped on the invoice.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="taxRates">Fixes the tax rate the invoice is raised at.</param>
    public InvoiceSelectedItemsHandler(
        IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency, IClock clock, ClientTaxRateResolver taxRates)
    {
        _context = context;
        _payerCurrency = payerCurrency;
        _clock = clock;
        _taxRates = taxRates;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The invoice selected items command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the newly created invoice ID; <c>BILLABLE_ITEM_NOT_FOUND</c> when an id names no
    /// billable item; <c>BILLABLE_ITEM_CLIENT_MISMATCH</c> when a named item belongs to a different
    /// client; or <c>BILLABLE_ITEM_INVOICED</c> when a named item is already on an invoice.
    /// </returns>
    public async Task<Result<int>> HandleAsync(InvoiceSelectedItemsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var items = await _context.BillableItems
            .Where(x => cmd.BillableItemIds.Contains(x.Id))
            .ToListAsync(ct);
        if (items.Count != cmd.BillableItemIds.Count)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.BILLABLE_ITEM_NOT_FOUND);
        }

        foreach (var item in items)
        {
            if (item.ClientId != cmd.ClientId)
            {
                return Result<int>.FailureCode(HostpanelErrorCodes.BILLABLE_ITEM_CLIENT_MISMATCH);
            }

            if (item.InvoiceId is not null)
            {
                return Result<int>.FailureCode(HostpanelErrorCodes.BILLABLE_ITEM_INVOICED);
            }
        }

        var currency = (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;
        var now = _clock.UtcNow;
        var invoice = Invoice.CreateForBillableItems(
            cmd.ClientId, now.Add(PaymentTerm), currency, items, now);

        var taxed = await _taxRates.ApplyToAsync(invoice, ct);
        if (taxed.IsFailure)
        {
            return Result<int>.FailureCode(taxed.ErrorCode!);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        foreach (var item in items)
        {
            item.MarkInvoiced(invoice.Id);
        }

        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(invoice.Id);
    }
}
