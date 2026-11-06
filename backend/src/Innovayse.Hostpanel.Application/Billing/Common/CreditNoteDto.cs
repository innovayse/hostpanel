using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A credit note as staff see it in a list or at the top of its page.</summary>
/// <param name="Id">Credit note primary key.</param>
/// <param name="Number">The note number such as <c>CN-0001</c>; null for a draft.</param>
/// <param name="Status">Lifecycle state.</param>
/// <param name="Method">How the value goes back to the client.</param>
/// <param name="ClientId">The client the note is issued to.</param>
/// <param name="ClientName">Display name of that client.</param>
/// <param name="InvoiceId">The invoice the note credits.</param>
/// <param name="Currency">ISO 4217 code every amount is in.</param>
/// <param name="SubTotal">Sum of the lines before tax.</param>
/// <param name="TaxRate">Tax percentage, copied from the invoice.</param>
/// <param name="Tax">Tax on the sub total.</param>
/// <param name="Total">What the note credits in all.</param>
/// <param name="Reason">Why the note was issued; printed on the note, so the client reads it.</param>
/// <param name="RefundReference">The gateway reference of the refund a gateway refund note paid out; null otherwise.</param>
/// <param name="CreatedAt">When the note was created.</param>
/// <param name="IssuedAt">When it got its number; null for a draft.</param>
/// <param name="AppliedAt">When its value was given to the client; null until then.</param>
/// <param name="VoidedAt">When it was voided; null while it stands.</param>
/// <param name="VoidReason">Why it was voided; null while it stands.</param>
public sealed record CreditNoteDto(
    int Id,
    string? Number,
    CreditNoteStatus Status,
    CreditNoteMethod Method,
    int ClientId,
    string ClientName,
    int InvoiceId,
    string Currency,
    decimal SubTotal,
    decimal TaxRate,
    decimal Tax,
    decimal Total,
    string Reason,
    string? RefundReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? AppliedAt,
    DateTimeOffset? VoidedAt,
    string? VoidReason);
