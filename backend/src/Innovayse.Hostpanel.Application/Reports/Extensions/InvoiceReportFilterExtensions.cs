using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Reports.Extensions;

/// <summary>
/// Shared filtering for the Invoices report, used by both the paged view
/// (<c>Queries/InvoicesReport</c>) and its CSV export (<c>Queries/ExportInvoicesReport</c>) so the
/// two always answer with exactly the same rows. Every day range is the half-open UTC window
/// <c>[from 00:00, to + 1 day 00:00)</c>.
/// </summary>
public static class InvoiceReportFilterExtensions
{
    /// <summary>Applies the Invoices report's optional filters to an invoice query.</summary>
    /// <param name="invoices">The base invoice query.</param>
    /// <param name="status">
    /// Invoice status to keep, matched case-insensitively; ignored (every status kept) when
    /// <see langword="null"/>, blank or not one of <see cref="InvoiceStatus"/>'s names.
    /// </param>
    /// <param name="createdFrom">Earliest creation day to include, inclusive.</param>
    /// <param name="createdTo">Latest creation day to include, inclusive.</param>
    /// <param name="dueFrom">Earliest due day to include, inclusive.</param>
    /// <param name="dueTo">Latest due day to include, inclusive.</param>
    /// <param name="paidFrom">Earliest paid day to include, inclusive. Invoices never paid never match.</param>
    /// <param name="paidTo">Latest paid day to include, inclusive. Invoices never paid never match.</param>
    /// <returns>The filtered query, not yet materialized.</returns>
    public static IQueryable<Invoice> ApplyReportFilters(
        this IQueryable<Invoice> invoices,
        string? status,
        DateOnly? createdFrom,
        DateOnly? createdTo,
        DateOnly? dueFrom,
        DateOnly? dueTo,
        DateOnly? paidFrom,
        DateOnly? paidTo)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var query = invoices;

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(i => i.Status == parsedStatus);
        }

        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value.StartOfDayUtc();
            query = query.Where(i => i.CreatedAt >= from);
        }

        if (createdTo.HasValue)
        {
            var toExclusive = createdTo.Value.EndOfDayExclusiveUtc();
            query = query.Where(i => i.CreatedAt < toExclusive);
        }

        if (dueFrom.HasValue)
        {
            var from = dueFrom.Value.StartOfDayUtc();
            query = query.Where(i => i.DueDate >= from);
        }

        if (dueTo.HasValue)
        {
            var toExclusive = dueTo.Value.EndOfDayExclusiveUtc();
            query = query.Where(i => i.DueDate < toExclusive);
        }

        if (paidFrom.HasValue)
        {
            var from = paidFrom.Value.StartOfDayUtc();
            query = query.Where(i => i.PaidAt != null && i.PaidAt >= from);
        }

        if (paidTo.HasValue)
        {
            var toExclusive = paidTo.Value.EndOfDayExclusiveUtc();
            query = query.Where(i => i.PaidAt != null && i.PaidAt < toExclusive);
        }

        return query;
    }
}
