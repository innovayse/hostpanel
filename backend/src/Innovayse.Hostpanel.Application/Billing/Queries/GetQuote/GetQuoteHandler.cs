using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetQuote;

/// <summary>Returns a single quote with its line items and client name.</summary>
public sealed class GetQuoteHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs; Billing never reads the client tables.</param>
    public GetQuoteHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The get quote query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the quote DTO, or <c>QUOTE_NOT_FOUND</c> when the id names no quote.</returns>
    public async Task<Result<QuoteDto>> HandleAsync(GetQuoteQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var quote = await _context.Quotes
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        if (quote is null)
        {
            return Result<QuoteDto>.FailureCode(HostpanelErrorCodes.QUOTE_NOT_FOUND);
        }

        var client = await _bus.GetRequiredClientBriefAsync(quote.ClientId, withEmail: false, ct);

        var itemDtos = quote.Items
            .Select(item => new QuoteItemDto(
                item.Id, item.Description, item.UnitPrice, item.Quantity, item.DiscountPercent, item.Taxed,
                item.AmountIn(quote.Currency)))
            .ToList();

        var dto = new QuoteDto(
            quote.Id,
            quote.ClientId,
            client.FullName,
            quote.Subject,
            quote.Stage,
            quote.CreatedAt,
            quote.ExpiryDate,
            quote.Notes,
            quote.ProposalText,
            quote.CustomerNotes,
            quote.AdminNotes,
            quote.Total,
            quote.Total,
            quote.Currency,
            itemDtos);

        return Result<QuoteDto>.Success(dto);
    }
}
