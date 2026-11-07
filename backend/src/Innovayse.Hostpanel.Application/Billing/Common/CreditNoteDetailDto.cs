namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A credit note with its lines, for its page.</summary>
/// <param name="Note">The note itself.</param>
/// <param name="Items">The lines credited, in their stored order.</param>
public sealed record CreditNoteDetailDto(CreditNoteDto Note, IReadOnlyList<CreditNoteItemDto> Items);
