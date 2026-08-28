namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Billing;

/// <summary>One quote, as the <c>quotes</c> section of a client data export lists it.</summary>
/// <param name="Id">The quote's primary key.</param>
/// <param name="Stage">Where the quote stands — Draft, Delivered, Accepted, Lost and so on.</param>
/// <param name="Total">Gross amount quoted.</param>
/// <param name="CreatedAt">When the quote was drawn up.</param>
/// <param name="ExpiresAt">When the quote stops being valid — the quote's expiry date.</param>
public sealed record ClientExportQuoteDto(
    int Id,
    QuoteStage Stage,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
