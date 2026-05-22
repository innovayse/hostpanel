namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a single row in paginated quote lists (no line items).</summary>
/// <param name="Id">Quote primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Subject">Quote subject / title.</param>
/// <param name="DateCreated">Creation timestamp (UTC).</param>
/// <param name="ValidUntil">Expiry date; null means no expiry.</param>
/// <param name="Total">Grand total of the quote.</param>
/// <param name="Stage">Current lifecycle stage.</param>
public record QuoteListItemDto(
    int Id,
    int ClientId,
    string Subject,
    DateTimeOffset DateCreated,
    DateTimeOffset? ValidUntil,
    decimal Total,
    QuoteStage Stage);
