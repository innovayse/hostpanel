namespace Innovayse.Application.Billing.Queries.GetQuote;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a single quote with its line items and client name.</summary>
public sealed class GetQuoteHandler(IQuoteRepository repo, IClientRepository clientRepo)
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
        var quote = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Quote {query.Id} not found.");

        var client = await clientRepo.FindByIdAsync(quote.ClientId, ct);
        var clientName = client != null ? $"{client.FirstName} {client.LastName}" : "Unknown";

        var itemDtos = quote.Items.Select(item => new QuoteItemDto(
            item.Id,
            item.Description,
            item.UnitPrice,
            item.Quantity,
            item.DiscountPercent,
            item.Taxed,
            item.Amount))
            .ToList();

        return new QuoteDto(
            quote.Id,
            quote.ClientId,
            clientName,
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
            itemDtos);
    }
}
