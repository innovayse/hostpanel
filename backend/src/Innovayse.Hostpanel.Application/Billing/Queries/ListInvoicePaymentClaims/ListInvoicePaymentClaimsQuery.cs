using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoicePaymentClaims;

/// <summary>Paginated, optionally filtered list of invoice payment claims: the admin view of held charges.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (max 100).</param>
/// <param name="Status">Optional status filter; <see langword="null"/> lists claims of every status.</param>
public sealed record ListInvoicePaymentClaimsQuery(
    int Page = 1,
    int PageSize = 25,
    InvoicePaymentClaimStatus? Status = null);
