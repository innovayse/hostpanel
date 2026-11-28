using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SettleFreeInvoice;

/// <summary>Handles <see cref="SettleFreeInvoiceCommand"/>: marks a nothing-owed invoice paid.</summary>
public sealed class SettleFreeInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the payment moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the payment moment is read from.</param>
    public SettleFreeInvoiceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The invoice to settle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice, or
    /// <c>INVOICE_NOT_PAYABLE</c> when it is not open, or <c>INVOICE_NOT_FREE</c> when it still has an amount due.
    /// </returns>
    public async Task<Result> HandleAsync(SettleFreeInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Payments are loaded because what is still due is the total less what was recorded.
        var invoice = await _context.Invoices
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (!invoice.IsPayable)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        if (invoice.AmountDue != 0)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FREE);
        }

        invoice.MarkPaid(PaymentMethodNames.NoCharge, _clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
