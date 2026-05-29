namespace Innovayse.Application.Billing.Queries.GetQuote;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns a full <see cref="QuoteDto"/> including line items.</summary>
public sealed class GetQuoteHandler(IQuoteRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetQuoteQuery"/>.
    /// </summary>
    /// <param name="query">The get quote query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The quote DTO with line items.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not found.</exception>
    public async Task<QuoteDto> HandleAsync(GetQuoteQuery query, CancellationToken ct)
    {
        var quote = await repo.FindByIdAsync(query.QuoteId, ct)
            ?? throw new InvalidOperationException($"Quote {query.QuoteId} not found.");

        return new QuoteDto(
            quote.Id,
            quote.ClientId,
            quote.Subject,
            quote.Stage,
            quote.DateCreated,
            quote.ValidUntil,
            quote.SubTotal,
            quote.Total,
            quote.ProposalText,
            quote.CustomerNotes,
            quote.AdminNotes,
            quote.Items.Select(i => new QuoteItemDto(
                i.Id, i.Quantity, i.Description, i.UnitPrice, i.DiscountPercent, i.Taxed, i.Amount))
                .ToList());
    }
}
