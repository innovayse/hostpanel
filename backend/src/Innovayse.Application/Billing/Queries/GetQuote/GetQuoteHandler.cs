namespace Innovayse.Application.Billing.Queries.GetQuote;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;
<<<<<<< HEAD
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a single quote with its line items and client name.</summary>
public sealed class GetQuoteHandler(IQuoteRepository repo, IClientRepository clientRepo)
=======

/// <summary>Returns a full <see cref="QuoteDto"/> including line items.</summary>
public sealed class GetQuoteHandler(IQuoteRepository repo)
>>>>>>> origin/main
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
<<<<<<< HEAD
        var quote = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Quote {query.Id} not found.");

        var client = await clientRepo.FindByIdAsync(quote.ClientId, ct);
        var clientName = client != null ? $"{client.FirstName} {client.LastName}" : "Unknown";

        var itemDtos = quote.Items.Select(item => new QuoteItemDto(
            item.Id,
            item.Description,
            item.UnitPrice,
            item.Quantity,
            item.Amount))
            .ToList();
=======
        var quote = await repo.FindByIdAsync(query.QuoteId, ct)
            ?? throw new InvalidOperationException($"Quote {query.QuoteId} not found.");
>>>>>>> origin/main

        return new QuoteDto(
            quote.Id,
            quote.ClientId,
<<<<<<< HEAD
            clientName,
            quote.Subject,
            quote.Status,
            quote.ExpiryDate,
            quote.Notes,
            quote.Total,
            quote.CreatedAt,
            itemDtos);
=======
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
>>>>>>> origin/main
    }
}
