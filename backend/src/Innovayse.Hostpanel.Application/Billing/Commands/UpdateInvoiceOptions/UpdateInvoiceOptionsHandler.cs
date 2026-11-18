using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.UpdateInvoiceOptions;

/// <summary>Updates invoice options (dates, payment method, tax rate).</summary>
public sealed class UpdateInvoiceOptionsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Returns credit the change left unneeded to the client's balance in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Decides whether a tax rate may be typed by hand for the invoice's client.</summary>
    private readonly ClientTaxRateResolver _taxRates;

    /// <summary>Stamps the moment a change that leaves the payments covering the invoice settles it.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Returns credit the change left unneeded to the client's balance in the same save.</param>
    /// <param name="taxRates">Decides whether a tax rate may be typed by hand for the invoice's client.</param>
    /// <param name="clock">Stamps the moment a change that leaves the payments covering the invoice settles it.</param>
    public UpdateInvoiceOptionsHandler(IHostpanelDbContext context, IClientCredit clientCredit, ClientTaxRateResolver taxRates, IClock clock)
    {
        _context = context;
        _clientCredit = clientCredit;
        _taxRates = taxRates;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The update options command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>INVOICE_NOT_FOUND</c> when the id names no invoice, <c>CLIENT_TAX_EXEMPT</c> or
    /// <c>CLIENT_REVERSE_CHARGE_APPLIES</c> when a tax rate is set on the invoice of a client whose tax is not typed
    /// by hand, or <c>CLIENT_NOT_FOUND</c> when
    /// excess credit has no client to go back to. A tax change below the payments taken throws; see
    /// <see cref="Domain.Billing.Invoice.UpdateOptions"/>.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateInvoiceOptionsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Items must be loaded: UpdateOptions recomputes the totals from the current line items,
        // and an untracked, empty collection would zero the sub-total on every save. Transactions
        // are loaded for the same reason: the payments taken bound how low the total may go.
        var invoice = await _context.Invoices
            .Include(x => x.Items)
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // Tax is only ever entered by hand here, so this is where a client whose tax is not the operator's to enter
        // (tax exempt, or invoiced under the reverse charge) is protected; the resolver is the one place that decides it.
        // A rate the invoice already carries may stay (an exemption granted later does not rewrite
        // history), but no new or higher rate is set.
        if (cmd.TaxRate > 0 && cmd.TaxRate != invoice.TaxRate)
        {
            var mayBeTaxed = await _taxRates.EnsureMayBeTaxedAsync(invoice.ClientId, ct);
            if (mayBeTaxed.IsFailure)
            {
                return mayBeTaxed;
            }
        }

        invoice.UpdateOptions(cmd.InvoiceDate, cmd.DueDate, cmd.PaymentMethod, cmd.TaxRate);

        // A lower charge can leave applied credit the invoice no longer needs; it goes back to the
        // balance in this save so credit is never parked under a total it exceeds.
        if (invoice.ExcessCredit > 0)
        {
            var returned = await _clientCredit.ReturnToBalanceAsync(invoice, invoice.ExcessCredit, ct);
            if (returned.IsFailure)
            {
                return returned;
            }
        }

        // A lower charge can leave the payments already taken covering the invoice: nothing is owed,
        // so it is settled in this save rather than left open to go overdue.
        invoice.SettleIfCovered(_clock.UtcNow);

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
