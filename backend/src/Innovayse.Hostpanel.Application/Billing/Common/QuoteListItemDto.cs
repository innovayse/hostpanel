using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;
/// <summary>DTO for a single row in paginated quote lists (no line items).</summary>
/// <param name="Id">Quote primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the owning client; every quote has one, since its client cannot be deleted.</param>
/// <param name="Subject">Quote subject / title.</param>
/// <param name="DateCreated">Creation timestamp (UTC).</param>
/// <param name="ValidUntil">Quote expiry date (UTC).</param>
/// <param name="Total">Grand total of the quote.</param>
/// <param name="Currency">ISO 4217 code the quote's amounts are in.</param>
/// <param name="Stage">Current lifecycle stage.</param>
public sealed record QuoteListItemDto(
    int Id,
    int ClientId,
    string ClientName,
    string Subject,
    DateTimeOffset DateCreated,
    DateTimeOffset ValidUntil,
    decimal Total,
    string Currency,
    QuoteStage Stage);
