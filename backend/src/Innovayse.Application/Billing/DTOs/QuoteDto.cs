namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a full quote with line items (admin detail view).</summary>
/// <param name="Id">Quote primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Subject">Quote subject/title.</param>
/// <param name="Stage">Current lifecycle stage.</param>
/// <param name="DateCreated">Creation timestamp (UTC).</param>
/// <param name="ValidUntil">Quote expiry date (UTC).</param>
/// <param name="Notes">Optional notes or terms.</param>
/// <param name="ProposalText">Optional proposal text shown at the top.</param>
/// <param name="CustomerNotes">Optional customer-facing footer notes.</param>
/// <param name="AdminNotes">Optional admin-only internal notes.</param>
/// <param name="SubTotal">Sum of all line item amounts before tax.</param>
/// <param name="Total">Grand total of the quote.</param>
/// <param name="Items">Line items on the quote.</param>
public record QuoteDto(
    int Id,
    int ClientId,
    string ClientName,
    string Subject,
    QuoteStage Stage,
    DateTimeOffset DateCreated,
    DateTimeOffset ValidUntil,
    string? Notes,
    string? ProposalText,
    string? CustomerNotes,
    string? AdminNotes,
    decimal SubTotal,
    decimal Total,
    IReadOnlyList<QuoteItemDto> Items);
