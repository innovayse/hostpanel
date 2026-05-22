namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO representing a full quote with its line items.</summary>
/// <param name="Id">Quote primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Subject">Quote subject / title.</param>
/// <param name="Stage">Current lifecycle stage.</param>
/// <param name="DateCreated">Creation timestamp (UTC).</param>
/// <param name="ValidUntil">Expiry date; null means no expiry.</param>
/// <param name="SubTotal">Sum of all line item amounts.</param>
/// <param name="Total">Grand total of the quote.</param>
/// <param name="ProposalText">Proposal text displayed at top; null if omitted.</param>
/// <param name="CustomerNotes">Customer-facing footer notes; null if omitted.</param>
/// <param name="AdminNotes">Private admin notes; null if omitted.</param>
/// <param name="Items">Line items on the quote.</param>
public record QuoteDto(
    int Id,
    int ClientId,
    string Subject,
    QuoteStage Stage,
    DateTimeOffset DateCreated,
    DateTimeOffset? ValidUntil,
    decimal SubTotal,
    decimal Total,
    string? ProposalText,
    string? CustomerNotes,
    string? AdminNotes,
    IReadOnlyList<QuoteItemDto> Items);
