using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoicePaymentClaims;

/// <summary>Returns a paginated, optionally filtered list of invoice payment claims for admin consumption.</summary>
public sealed class ListInvoicePaymentClaimsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs; Billing never reads the client tables.</param>
    public ListInvoicePaymentClaimsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ListInvoicePaymentClaimsQuery"/>.</summary>
    /// <param name="query">The page, page size and optional status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of claims, newest first. Never refuses.</returns>
    public async Task<Result<PaginatedList<InvoicePaymentClaimDto>>> HandleAsync(
        ListInvoicePaymentClaimsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        IQueryable<InvoicePaymentClaim> filtered = _context.InvoicePaymentClaims.AsNoTracking();
        if (query.Status is { } status)
        {
            filtered = filtered.Where(c => c.Status == status);
        }

        // Id breaks ties so two claims written in the same instant never swap places between pages.
        var ordered = filtered.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered.Paginate(page, pageSize).ToListAsync(ct);

        var clientIds = items.Select(c => c.ClientId).Distinct().ToList();
        var clientMap = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = items
            .Select(c => new InvoicePaymentClaimDto(
                c.Id,
                c.InvoiceId,
                c.ClientId,
                clientMap[c.ClientId].FullName,
                c.GatewayModuleId,
                CurrencyCode.FromMinorUnits(c.AmountMinor, c.Currency),
                c.Currency,
                c.Status,
                c.TransactionId,
                c.CreatedAt,
                c.ResolvedAt))
            .ToList();

        return Result<PaginatedList<InvoicePaymentClaimDto>>.Success(
            new PaginatedList<InvoicePaymentClaimDto>(dtos, total, page, pageSize));
    }
}
