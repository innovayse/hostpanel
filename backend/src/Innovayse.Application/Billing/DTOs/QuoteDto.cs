namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a quote in admin lists.</summary>
/// <param name="Id">Quote primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Subject">Quote subject/title.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="ExpiryDate">Quote expiry date (UTC).</param>
/// <param name="Notes">Optional notes or terms.</param>
/// <param name="Total">Sum of all line item amounts.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="Items">Line items on the quote.</param>
public record QuoteDto(
    int Id,
    int ClientId,
    string ClientName,
    string Subject,
    QuoteStatus Status,
    DateTimeOffset ExpiryDate,
    string? Notes,
    decimal Total,
    DateTimeOffset CreatedAt,
    IReadOnlyList<QuoteItemDto> Items);
