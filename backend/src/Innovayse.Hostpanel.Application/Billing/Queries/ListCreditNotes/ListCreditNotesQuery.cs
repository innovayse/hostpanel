using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListCreditNotes;

/// <summary>Paginated, searchable, filterable query for credit notes: staff view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only notes whose number, reason, refund reference or id contains it, or that belong to a client whose first
/// name, last name, company, sign-in address or account name contains it (case-insensitive, a <c>%</c> or <c>_</c> matches
/// itself), are listed. Blank lists all.
/// </param>
/// <param name="InvoiceId">Optional filter: only the notes of this invoice.</param>
/// <param name="ClientId">Optional filter: only the notes of this client.</param>
/// <param name="Status">Optional status filter, bound by name from the query string; <see langword="null"/> for every status.</param>
/// <param name="Method">Optional method filter, bound by name from the query string; <see langword="null"/> for every method.</param>
public sealed record ListCreditNotesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? InvoiceId = null,
    int? ClientId = null,
    CreditNoteStatus? Status = null,
    CreditNoteMethod? Method = null) : IListQuery;
