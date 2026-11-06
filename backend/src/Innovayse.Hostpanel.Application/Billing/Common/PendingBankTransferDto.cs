namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One row of the staff list of manual bank transfers that wait to be confirmed or rejected.</summary>
/// <param name="InvoiceId">The invoice the transfer pays.</param>
/// <param name="Reference">The reference the client was told to write on the transfer.</param>
/// <param name="ClientId">The invoiced client.</param>
/// <param name="ClientName">Full name of the invoiced client.</param>
/// <param name="Total">The invoice total, in <paramref name="Currency"/>.</param>
/// <param name="AmountDue">What the transfer must be for, in <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code the invoice is billed in.</param>
/// <param name="DueDate">The invoice's payment due date (UTC).</param>
/// <param name="RequestedAt">When the client chose the transfer (UTC).</param>
public sealed record PendingBankTransferDto(
    int InvoiceId,
    string Reference,
    int ClientId,
    string ClientName,
    decimal Total,
    decimal AmountDue,
    string Currency,
    DateTimeOffset DueDate,
    DateTimeOffset RequestedAt);
