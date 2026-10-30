using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.LapseOverdueInvoice;

/// <summary>
/// Marks an overdue invoice lapsed when every digital renewal line on it is past its product's grace period.
/// Marking the invoice raises the lapse event; the fulfilment records are written with that save, not here.
/// </summary>
public sealed class LapseOverdueInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the current moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public LapseOverdueInvoiceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The lapse command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the invoice was marked lapsed, <see langword="false"/> when nothing was marked because it is no longer overdue, has lapsed already or
    /// still has a digital renewal line within grace; or <c>INVOICE_NOT_FOUND</c> when the id names no invoice.
    /// </returns>
    public async Task<Result<bool>> HandleAsync(LapseOverdueInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<bool>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // The state is read again here, in this invoice's own scope: it may have been paid or lapsed since the sweep listed it.
        var now = _clock.UtcNow;
        if (!invoice.CanLapse || !invoice.IsPastDigitalGrace(now))
        {
            return Result<bool>.Success(false);
        }

        invoice.MarkLapsed(now);
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
