using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetServiceRenewalInvoice;

/// <summary>Handles <see cref="GetServiceRenewalInvoiceQuery"/>: one read of the invoices billing the period.</summary>
public sealed class GetServiceRenewalInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetServiceRenewalInvoiceHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Finds the invoice.</summary>
    /// <param name="query">The service and the period start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newest invoice that is not cancelled and has a renewal line for the service and period start, or
    /// <see langword="null"/> when there is none. Never refuses.
    /// </returns>
    public async Task<Result<ServiceRenewalInvoiceDto?>> HandleAsync(GetServiceRenewalInvoiceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoice = await _context.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Cancelled
                        && i.Items.Any(l => l.ClientServiceId == query.ServiceId && l.RenewalPeriodStart == query.PeriodStart))
            .OrderByDescending(i => i.Id)
            .Select(i => new ServiceRenewalInvoiceDto(i.Id, i.Status))
            .FirstOrDefaultAsync(ct);

        return Result<ServiceRenewalInvoiceDto?>.Success(invoice);
    }
}
