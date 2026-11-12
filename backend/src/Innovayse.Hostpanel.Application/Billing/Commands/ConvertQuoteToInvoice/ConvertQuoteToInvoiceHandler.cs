using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
namespace Innovayse.Hostpanel.Application.Billing.Commands.ConvertQuoteToInvoice;

/// <summary>
/// Converts a quote into a draft invoice by copying its line items. The quote stage is set to
/// <see cref="QuoteStage.Accepted"/> after conversion.
/// </summary>
public sealed class ConvertQuoteToInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the issue moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Fixes the tax rate the invoice is raised at.</summary>
    private readonly ClientTaxRateResolver _taxRates;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the issue moment is read from.</param>
    /// <param name="taxRates">Fixes the tax rate the invoice is raised at.</param>
    public ConvertQuoteToInvoiceHandler(IHostpanelDbContext context, IClock clock, ClientTaxRateResolver taxRates)
    {
        _context = context;
        _clock = clock;
        _taxRates = taxRates;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The convert command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the ID of the newly created draft invoice, <c>QUOTE_NOT_FOUND</c> when the
    /// quote does not exist, or <c>CLIENT_NOT_FOUND</c> when its client does not. The quote's own stage invariant still throws:
    /// see <see cref="Quote.Accept"/>.
    /// </returns>
    public async Task<Result<int>> HandleAsync(ConvertQuoteToInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var quote = await _context.Quotes
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == cmd.QuoteId, ct);
        if (quote is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.QUOTE_NOT_FOUND);
        }

        var itemData = quote.GetInvoiceItemData();
        // The quote's prices are in the quote's own currency, so the invoice bills in it too.
        var invoice = Invoice.CreateDraft(quote.ClientId, quote.ExpiryDate, quote.Currency, _clock.UtcNow);

        foreach (var (description, unitPrice, quantity) in itemData)
        {
            invoice.AddItem(description, unitPrice, quantity);
        }

        var taxed = await _taxRates.ApplyToAsync(invoice, ct);
        if (taxed.IsFailure)
        {
            return Result<int>.FailureCode(taxed.ErrorCode!);
        }

        _context.Invoices.Add(invoice);
        quote.Accept();

        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(invoice.Id);
    }
}
