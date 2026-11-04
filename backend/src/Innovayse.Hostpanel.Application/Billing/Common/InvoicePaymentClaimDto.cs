using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One invoice payment claim as the admin sees it: a stored method charge and where it stands.</summary>
/// <param name="Id">The claim's primary key.</param>
/// <param name="InvoiceId">The invoice the charge is for.</param>
/// <param name="ClientId">The client whose method was charged.</param>
/// <param name="ClientName">Full name of the client.</param>
/// <param name="GatewayModuleId">The id of the payment plugin that was asked to charge.</param>
/// <param name="Amount">The amount asked of the gateway, in whole units of <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code of the amount.</param>
/// <param name="Status">Where the charge stands.</param>
/// <param name="TransactionId">The gateway's transaction reference once it took money, otherwise <see langword="null"/>.</param>
/// <param name="CreatedAt">When the claim was written (UTC).</param>
/// <param name="ResolvedAt">When the claim reached a final status (UTC), or <see langword="null"/> while unresolved.</param>
public sealed record InvoicePaymentClaimDto(
    int Id,
    int InvoiceId,
    int ClientId,
    string ClientName,
    string GatewayModuleId,
    decimal Amount,
    string Currency,
    InvoicePaymentClaimStatus Status,
    string? TransactionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
