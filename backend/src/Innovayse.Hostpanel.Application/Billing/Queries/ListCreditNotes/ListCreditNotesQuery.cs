using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListCreditNotes;

/// <summary>Paginated, optionally filtered query for credit notes: staff view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (clamped by the handler).</param>
/// <param name="InvoiceId">Optional filter: only the notes of this invoice.</param>
/// <param name="ClientId">Optional filter: only the notes of this client.</param>
/// <param name="Status">Optional status filter.</param>
public sealed record ListCreditNotesQuery(
    int Page = 1,
    int PageSize = 25,
    int? InvoiceId = null,
    int? ClientId = null,
    CreditNoteStatus? Status = null);
