using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListCreditNotes;

/// <summary>Lists credit notes for staff, newest first, optionally of one invoice, one client or one status.</summary>
public sealed class ListCreditNotesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client names.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client names.</param>
    public ListCreditNotesHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ListCreditNotesQuery"/>.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of credit notes. Never refuses.</returns>
    public async Task<Result<PaginatedList<CreditNoteDto>>> HandleAsync(ListCreditNotesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        var filtered = _context.CreditNotes.AsNoTracking();
        if (query.InvoiceId is { } invoiceId)
        {
            filtered = filtered.Where(n => n.InvoiceId == invoiceId);
        }

        if (query.ClientId is { } clientId)
        {
            filtered = filtered.Where(n => n.ClientId == clientId);
        }

        if (query.Status is { } status)
        {
            filtered = filtered.Where(n => n.Status == status);
        }

        var ordered = filtered.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);
        var total = await ordered.CountAsync(ct);
        var notes = await ordered.Paginate(page, pageSize).ToListAsync(ct);

        var clients = await _bus.GetRequiredClientBriefsByIdAsync(notes.Select(n => n.ClientId).Distinct().ToList(), ct);
        var rows = notes.Select(n => CreditNoteMapper.ToDto(n, clients[n.ClientId].FullName)).ToList();

        return Result.Success(new PaginatedList<CreditNoteDto>(rows, total, page, pageSize));
    }
}
