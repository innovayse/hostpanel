using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoiceFulfilment;

/// <summary>Handles <see cref="ListInvoiceFulfilmentQuery"/>: one read of an invoice's fulfilment records.</summary>
public sealed class ListInvoiceFulfilmentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListInvoiceFulfilmentHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The invoice whose records to list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The invoice's records, oldest first (none when no plugin was told anything yet), or
    /// <c>INVOICE_NOT_FOUND</c> when the id names no invoice.
    /// </returns>
    public async Task<Result<IReadOnlyList<FulfilmentRecordDto>>> HandleAsync(
        ListInvoiceFulfilmentQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _context.Invoices.AnyAsync(i => i.Id == query.InvoiceId, ct))
        {
            return Result<IReadOnlyList<FulfilmentRecordDto>>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        var records = await _context.FulfilmentRecords
            .AsNoTracking()
            .Where(r => r.InvoiceId == query.InvoiceId)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        // Projected after the read: CanRetry is the entity's own rule, not a column the database could filter on.
        return Result.Success<IReadOnlyList<FulfilmentRecordDto>>(records
            .Select(r => new FulfilmentRecordDto(
                r.Id, r.InvoiceId, r.PluginId, r.Kind, r.Status, r.Attempts, r.NextAttemptAt, r.LastError,
                r.CompletedAt, r.CreatedAt, r.CanRetry))
            .ToList());
    }
}
