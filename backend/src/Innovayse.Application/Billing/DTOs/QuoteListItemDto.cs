namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a single row in paginated quote lists (no line items).</summary>
/// <param name="Id">Quote primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the owning client.</param>
/// <param name="Subject">Quote subject / title.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="ExpiryDate">Quote expiry date (UTC).</param>
/// <param name="Total">Grand total of the quote.</param>
/// <param name="Status">Current lifecycle status.</param>
public record QuoteListItemDto(
    int Id,
    int ClientId,
    string ClientName,
    string Subject,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiryDate,
    decimal Total,
    QuoteStatus Status);
