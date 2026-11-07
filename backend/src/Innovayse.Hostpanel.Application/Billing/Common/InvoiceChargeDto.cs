using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Where a stored payment method charge of an invoice stands.</summary>
/// <param name="ClaimId">The claim that tracks the charge.</param>
/// <param name="InvoiceId">The invoice charged.</param>
/// <param name="Status">Where the charge stands. <see cref="InvoicePaymentClaimStatus.Settled"/> means the invoice is paid.</param>
/// <param name="ActionUrl">Where to send the payer to authenticate; set only while the status is <see cref="InvoicePaymentClaimStatus.ActionRequired"/>.</param>
public sealed record InvoiceChargeDto(int ClaimId, int InvoiceId, InvoicePaymentClaimStatus Status, string? ActionUrl);
